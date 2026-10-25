using System.Text.Json;
using BS2.Application;
using BS2.Application.Abstractions;
using BS2.Application.Banking;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BS2.Tests.Banking;

public class EdenredImportServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeEncryptor _encryptor = new();
    private readonly FakeClassifier _classifier = new();

    private EdenredImportService CreateSut(IAppDbContext db) => new(
        db, new StaticCurrentUser(TestEnv.UserId), _classifier, _encryptor, _clock,
        Options.Create(new ClassificationOptions { ClassifyOnSync = true }), NullLogger<EdenredImportService>.Instance);

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement;

    private const string Operations = """
        {"meta":{"status":"succeeded"},"data":[
          {"operation_ref":"1","status":"success","movement":"balance_debit","type":"redemption","currency":"EUR","date":"2026-07-19T19:05:34",
           "outlet":{"outlet_name":"CARREFOUR MARKET MELLE"},"transaction_details":{"product_ref":"TRE","amount":-1310}},
          {"operation_ref":"2","status":"success","movement":"balance_credit","type":"top-up","currency":"EUR","date":"2026-07-01T08:00:00",
           "reason":"22 * 9,15Euro","transaction_details":{"product_ref":"TRE","amount":20130}},
          {"operation_ref":"3","status":"declined","type":"redemption","date":"2026-07-02T08:00:00","transaction_details":{"amount":-500}}
        ]}
        """;

    [Fact]
    public async Task Maps_operations_skips_declined_and_dedups_on_reimport()
    {
        await using var db = TestEnv.NewDb(_encryptor);

        var first = await CreateSut(db).ImportAsync(Body(Operations));
        var second = await CreateSut(db).ImportAsync(Body(Operations));

        Assert.Equal(new EdenredImportResult(2, 2, 2), first);
        Assert.Equal(new EdenredImportResult(2, 0, 0), second);

        var connection = await db.BankConnections.Include(c => c.Accounts).SingleAsync();
        Assert.Equal(BankProvider.Edenred, connection.Provider);
        Assert.Equal(_clock.UtcNow, connection.LastSyncedAt);
        Assert.Equal("Edenred", connection.Accounts.Single().Identifier);

        var spend = await db.Transactions.SingleAsync(t => t.ExternalId == "EDENRED-1");
        Assert.Equal(-13.10m, spend.Amount);
        Assert.Equal(new DateOnly(2026, 7, 19), spend.Date);
        Assert.Equal("CARREFOUR MARKET MELLE", spend.CounterpartyName);
        Assert.Equal("Meal voucher · redemption", spend.Description);
        Assert.Equal(TransactionSource.Edenred, spend.Source);

        var topUp = await db.Transactions.SingleAsync(t => t.ExternalId == "EDENRED-2");
        Assert.Equal(201.30m, topUp.Amount);
        Assert.Equal("Meal voucher · top-up · 22 * 9,15Euro", topUp.Description);
    }

    [Fact]
    public async Task Moves_the_excel_imported_edenred_account_and_skips_its_rows()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        var imported = new BankConnection { Id = Guid.NewGuid(), UserId = TestEnv.UserId, Provider = BankProvider.Import, BankName = "Edenred", Country = "BE" };
        var account = new Account { Id = Guid.NewGuid(), UserId = TestEnv.UserId, BankConnectionId = imported.Id, Identifier = "Edenred", IdentifierHash = _encryptor.Hash("Edenred") };
        db.AddRange(imported, account, new Transaction { Id = Guid.NewGuid(), UserId = TestEnv.UserId, AccountId = account.Id, ExternalId = "EDENRED-1", Amount = -13.10m });
        await db.SaveChangesAsync();

        var result = await CreateSut(db).ImportAsync(Body(Operations));

        Assert.Equal(1, result.New);
        var moved = await db.Accounts.SingleAsync();
        Assert.Equal(BankProvider.Edenred, (await db.BankConnections.SingleAsync(c => c.Id == moved.BankConnectionId)).Provider);
        Assert.All(await db.Transactions.ToListAsync(), t => Assert.Equal(account.Id, t.AccountId));
    }
}
