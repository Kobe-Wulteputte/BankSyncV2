using Xunit;
using BS2.Application.Abstractions;
using BS2.Application.Banking;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BS2.Tests.Banking;

public class AuthorizationFlowServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeEncryptor _encryptor = new();
    private readonly FakeBankingProvider _provider = new();

    private AuthorizationFlowService CreateSut(IAppDbContext db) =>
        new(db, _provider, _encryptor, _clock, TestEnv.SyncOptions(), TestEnv.AppOptions(), NullLogger<AuthorizationFlowService>.Instance);

    [Fact]
    public async Task Complete_happy_path_upserts_accounts_removes_pending_and_deletes_old_session()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock, sessionId: "sess-old", ibans: ["BE68539007547034"]);
        var existing = TestEnv.NewAccount(connection, "BE68539007547034", "acc-old", _encryptor, _clock);
        var pending = new PendingAuthorization
        {
            Id = Guid.NewGuid(), UserId = TestEnv.UserId, BankConnectionId = connection.Id, State = "state-1", Url = "u",
            CreatedAt = _clock.UtcNow, ExpiresAt = _clock.UtcNow.AddHours(24)
        };
        db.AddRange(connection, existing, pending);
        await db.SaveChangesAsync();

        var validUntil = _clock.UtcNow.AddDays(90);
        _provider.CompleteResult = new ProviderSession("sess-new", "Revolut", "BE", validUntil,
        [
            new ProviderAccount("acc-1", "BE68 5390 0754 7034", true, "Main", "EUR"),
            new ProviderAccount("acc-2", "BE71096123456769", true, "Savings", "EUR")
        ]);

        var result = await CreateSut(db).CompleteAsync("code", "state-1", TestEnv.UserId, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(TestEnv.UserId, result.UserId);
        Assert.Equal("Revolut", result.BankName);

        var saved = await db.BankConnections.Include(c => c.Accounts).SingleAsync();
        Assert.Equal("sess-new", saved.ExternalSessionId);
        Assert.Equal(ConnectionStatus.Active, saved.Status);
        Assert.Equal(validUntil, saved.ValidUntil);
        Assert.Equal(_clock.UtcNow, saved.LastAuthorizedAt);

        Assert.Equal(2, saved.Accounts.Count);
        var repointed = saved.Accounts.Single(a => a.Identifier == "BE68539007547034");
        Assert.Equal(existing.Id, repointed.Id);
        Assert.Equal("acc-1", repointed.ExternalAccountId);
        Assert.Equal("Main", repointed.DisplayName);
        Assert.Equal("acc-2", saved.Accounts.Single(a => a.Identifier == "BE71096123456769").ExternalAccountId);

        Assert.Empty(await db.PendingAuthorizations.ToListAsync());
        Assert.Equal(["sess-old"], _provider.DeletedSessions);
    }

    private static PendingAuthorization Pending(BankConnection c, FakeClock clock, TimeSpan ttl) => new()
    {
        Id = Guid.NewGuid(), UserId = TestEnv.UserId, BankConnectionId = c.Id, State = "s", Url = "u",
        CreatedAt = clock.UtcNow, ExpiresAt = clock.UtcNow + ttl
    };

    [Fact]
    public async Task Complete_returns_failed_result_when_provider_throws_unexpected_exception()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock);
        db.AddRange(connection, Pending(connection, _clock, TimeSpan.FromHours(1)));
        await db.SaveChangesAsync();
        _provider.CompleteError = new HttpRequestException("network down");

        var result = await CreateSut(db).CompleteAsync("code", "s", TestEnv.UserId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.DoesNotContain("network down", result.Message);
    }

    [Fact]
    public async Task Complete_rejects_state_started_by_another_user_and_keeps_it_for_the_owner()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock);
        db.AddRange(connection, Pending(connection, _clock, TimeSpan.FromHours(1)));
        await db.SaveChangesAsync();
        _provider.CompleteResult = new ProviderSession("sess-new", "Revolut", "BE", null, []);

        var result = await CreateSut(db).CompleteAsync("code", "s", Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.UserId);
        Assert.Equal(0, _provider.CompleteCalls);
        Assert.Single(await db.PendingAuthorizations.ToListAsync());
    }

    [Fact]
    public async Task Complete_rejects_expired_pending_and_removes_it()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock);
        db.AddRange(connection, Pending(connection, _clock, TimeSpan.Zero));
        await db.SaveChangesAsync();
        _provider.CompleteResult = new ProviderSession("sess-new", "Revolut", "BE", null, []);

        var result = await CreateSut(db).CompleteAsync("code", "s", TestEnv.UserId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("expired", result.Message);
        Assert.Equal(0, _provider.CompleteCalls);
        Assert.Empty(await db.PendingAuthorizations.ToListAsync());
    }

    [Fact]
    public async Task Complete_clears_external_id_of_accounts_missing_from_new_session()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock);
        var kept = TestEnv.NewAccount(connection, "BE68539007547034", "acc-old-1", _encryptor, _clock);
        var dead = TestEnv.NewAccount(connection, "BE71096123456769", "acc-old-2", _encryptor, _clock);
        db.AddRange(connection, kept, dead, Pending(connection, _clock, TimeSpan.FromHours(1)));
        await db.SaveChangesAsync();
        _provider.CompleteResult = new ProviderSession("sess-new", "Revolut", "BE", null,
            [new ProviderAccount("acc-new-1", "BE68539007547034", true, "Main", "EUR")]);

        var result = await CreateSut(db).CompleteAsync("code", "s", TestEnv.UserId, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        var accounts = await db.Accounts.ToListAsync();
        Assert.Equal("acc-new-1", accounts.Single(a => a.Id == kept.Id).ExternalAccountId);
        var stale = accounts.Single(a => a.Id == dead.Id);
        Assert.Null(stale.ExternalAccountId);
        Assert.True(stale.IsActive);
    }

    [Fact]
    public async Task Complete_rejects_unknown_state_without_calling_provider()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        _provider.CompleteResult = new ProviderSession("sess-new", "Revolut", "BE", null, []);

        var result = await CreateSut(db).CompleteAsync("code", "never-issued", TestEnv.UserId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.UserId);
        Assert.Equal(0, _provider.CompleteCalls);
    }

    [Fact]
    public async Task EnsurePending_reuses_outstanding_link_and_replaces_expired_one()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var connection = TestEnv.NewConnection(_clock, status: ConnectionStatus.NotAuthorized, sessionId: null);
        var outstanding = new PendingAuthorization
        {
            Id = Guid.NewGuid(), UserId = TestEnv.UserId, BankConnectionId = connection.Id, State = "state-1", Url = "https://bank.example/first",
            CreatedAt = _clock.UtcNow.AddHours(-1), ExpiresAt = _clock.UtcNow.AddHours(23)
        };
        db.AddRange(connection, outstanding);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var (reused, created) = await sut.EnsurePendingAsync(connection, CancellationToken.None);

        Assert.False(created);
        Assert.Equal("state-1", reused.State);
        Assert.Empty(_provider.StartedStates);

        _clock.UtcNow = _clock.UtcNow.AddHours(24);
        var (fresh, createdNow) = await sut.EnsurePendingAsync(connection, CancellationToken.None);

        Assert.True(createdNow);
        Assert.Single(_provider.StartedStates);
        Assert.Equal(_provider.StartedStates[0], fresh.State);
        Assert.Equal(_clock.UtcNow.AddHours(24), fresh.ExpiresAt);
        Assert.Equal([fresh.State], await db.PendingAuthorizations.Select(p => p.State).ToListAsync());
    }
}
