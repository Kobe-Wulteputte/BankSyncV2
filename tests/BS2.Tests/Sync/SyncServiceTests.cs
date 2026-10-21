using Xunit;
using BS2.Application;
using BS2.Application.Abstractions;
using BS2.Application.Sync;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using BS2.Tests.Banking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BS2.Tests.Sync;

public class SyncServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeEncryptor _encryptor = new();
    private readonly FakeBankingProvider _provider = new();
    private readonly FakeClassifier _classifier = new();

    private SyncService CreateSut(IAppDbContext db) => new(
        db, _provider, _classifier, _clock, new NoSyncSchedule(), TestEnv.SyncOptions(),
        Options.Create(new ClassificationOptions { ClassifyOnSync = true }), NullLogger<SyncService>.Instance);

    [Fact]
    public async Task Dedups_existing_external_ids_and_classifies_only_new_rows()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock);
        var account = TestEnv.NewAccount(connection, "BE68539007547034", "acc-1", _encryptor, _clock);
        db.AddRange(connection, account, new Transaction
        {
            Id = Guid.NewGuid(), UserId = TestEnv.UserId, AccountId = account.Id, Source = TransactionSource.EnableBanking,
            ExternalId = "t1", Amount = -1, Date = new DateOnly(2026, 9, 1), CreatedAt = _clock.UtcNow, UpdatedAt = _clock.UtcNow
        });
        await db.SaveChangesAsync();
        _provider.Transactions["acc-1"] = [TestEnv.Tx("t1"), TestEnv.Tx("t2"), TestEnv.Tx("t2")];

        var run = await CreateSut(db).RunAsync(TestEnv.UserId, SyncTrigger.Manual, CancellationToken.None);

        Assert.Equal(SyncStatus.Succeeded, run.Status);
        Assert.Equal(3, run.TransactionsFetched);
        Assert.Equal(1, run.TransactionsNew);
        Assert.Equal(1, run.TransactionsClassified);
        Assert.Equal(1, run.ConnectionsSynced);

        var all = await db.Transactions.ToListAsync();
        Assert.Equal(2, all.Count);
        var added = all.Single(t => t.ExternalId == "t2");
        Assert.Equal([added.Id], _classifier.Classified);
        Assert.Equal(new DateOnly(2026, 9, 30), added.Date); // earlier of value/booking date
        Assert.Equal(_clock.UtcNow, (await db.BankConnections.SingleAsync()).LastSyncedAt);
        Assert.Equal("Synced", run.Details.Single().Status);
    }

    [Fact]
    public async Task Skips_expired_connection_with_detail_entry()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock, validUntil: _clock.UtcNow.AddMinutes(-1));
        db.AddRange(connection, TestEnv.NewAccount(connection, "BE68539007547034", "acc-1", _encryptor, _clock));
        await db.SaveChangesAsync();

        var run = await CreateSut(db).RunAsync(TestEnv.UserId, SyncTrigger.Scheduled, CancellationToken.None);

        Assert.Equal(SyncStatus.PartiallySucceeded, run.Status);
        Assert.Equal(1, run.ConnectionsSkipped);
        Assert.Equal(0, run.ConnectionsSynced);
        var detail = Assert.Single(run.Details);
        Assert.Equal("Skipped", detail.Status);
        Assert.Contains("expired", detail.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_provider.FetchedAccounts);
        Assert.Equal(ConnectionStatus.Expired, (await db.BankConnections.SingleAsync()).Status);
        Assert.NotNull(run.FinishedAt);
    }

    [Fact]
    public async Task Configured_ibans_filter_accounts_and_warn_for_uncovered_ones()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock, ibans: ["BE68539007547034", "BE00000000000000"]);
        db.AddRange(connection,
            TestEnv.NewAccount(connection, "BE68539007547034", "acc-a", _encryptor, _clock),
            TestEnv.NewAccount(connection, "BE71096123456769", "acc-b", _encryptor, _clock));
        await db.SaveChangesAsync();
        _provider.Transactions["acc-a"] = [TestEnv.Tx("a1")];
        _provider.Transactions["acc-b"] = [TestEnv.Tx("b1")];

        var run = await CreateSut(db).RunAsync(TestEnv.UserId, SyncTrigger.Manual, CancellationToken.None);

        Assert.Equal(["acc-a"], _provider.FetchedAccounts);
        Assert.Equal(1, run.TransactionsNew);
        Assert.Contains("BE00000000000000", (await db.BankConnections.SingleAsync()).LastSyncError);
    }

    [Fact]
    public async Task No_configured_ibans_syncs_every_active_account()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock);
        var inactive = TestEnv.NewAccount(connection, "BE00000000000000", "acc-off", _encryptor, _clock);
        inactive.IsActive = false;
        db.AddRange(connection,
            TestEnv.NewAccount(connection, "BE68539007547034", "acc-a", _encryptor, _clock),
            TestEnv.NewAccount(connection, "BE71096123456769", "acc-b", _encryptor, _clock),
            inactive);
        await db.SaveChangesAsync();

        var run = await CreateSut(db).RunAsync(TestEnv.UserId, SyncTrigger.Manual, CancellationToken.None);

        Assert.Equal(["acc-a", "acc-b"], _provider.FetchedAccounts.Order());
        Assert.Null((await db.BankConnections.SingleAsync()).LastSyncError);
        Assert.Equal(SyncStatus.Succeeded, run.Status);
    }

    [Fact]
    public async Task Unexpected_provider_exception_fails_the_account_not_the_run_loop()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock);
        db.AddRange(connection, TestEnv.NewAccount(connection, "BE68539007547034", "acc-1", _encryptor, _clock));
        await db.SaveChangesAsync();
        _provider.TransactionsError = new InvalidOperationException("boom");

        var run = await CreateSut(db).RunAsync(TestEnv.UserId, SyncTrigger.Manual, CancellationToken.None);

        Assert.Equal(SyncStatus.Failed, run.Status);
        Assert.Equal("Failed", Assert.Single(run.Details).Status);
        Assert.Contains("boom", (await db.BankConnections.SingleAsync()).LastSyncError);
    }

    [Fact]
    public async Task Provider_rejection_with_dead_session_marks_connection_expired()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock);
        db.AddRange(connection, TestEnv.NewAccount(connection, "BE68539007547034", "acc-1", _encryptor, _clock));
        await db.SaveChangesAsync();
        _provider.TransactionsError = new BankingProviderException("Session is expired");
        _provider.SessionResult = null;

        await CreateSut(db).RunAsync(TestEnv.UserId, SyncTrigger.Scheduled, CancellationToken.None);

        Assert.Equal(ConnectionStatus.Expired, (await db.BankConnections.SingleAsync()).Status);
    }

    [Fact]
    public async Task Failed_save_marks_run_failed_instead_of_leaving_it_running()
    {
        await using var db = new FailingSaveDb(_encryptor, failOnCall: 2);
        var connection = TestEnv.NewConnection(_clock);
        var account = TestEnv.NewAccount(connection, "BE68539007547034", "acc-1", _encryptor, _clock);
        db.AddRange(connection, account);
        await db.SaveChangesAsync();
        db.Calls = 0;
        _provider.Transactions["acc-1"] = [TestEnv.Tx("t1")];

        var run = await CreateSut(db).RunAsync(TestEnv.UserId, SyncTrigger.Manual, CancellationToken.None);

        Assert.Equal(SyncStatus.Failed, run.Status);
        var stored = await db.SyncRuns.SingleAsync();
        Assert.Equal(SyncStatus.Failed, stored.Status);
        Assert.NotNull(stored.FinishedAt);
        Assert.Empty(db.Transactions);
    }

    private sealed class FailingSaveDb(FakeEncryptor encryptor, int failOnCall) : BS2.Infrastructure.Persistence.AppDbContext(
        new DbContextOptionsBuilder<BS2.Infrastructure.Persistence.AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, encryptor)
    {
        public int Calls { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            ++Calls == failOnCall ? throw new DbUpdateException("unique violation") : base.SaveChangesAsync(cancellationToken);
    }

    [Fact]
    public async Task Rejects_second_run_while_one_is_in_progress()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        db.SyncRuns.Add(new SyncRun { Id = Guid.NewGuid(), UserId = TestEnv.UserId, Status = SyncStatus.Running, StartedAt = _clock.UtcNow.AddMinutes(-5) });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BS2.Application.Common.ConflictException>(() =>
            CreateSut(db).RunAsync(TestEnv.UserId, SyncTrigger.Manual, CancellationToken.None));
    }
}
