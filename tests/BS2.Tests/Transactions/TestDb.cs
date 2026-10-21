using BS2.Application.Abstractions;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using BS2.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BS2.Tests.Transactions;

internal sealed class PassThroughEncryptor : IFieldEncryptor
{
    public string Encrypt(string plaintext) => plaintext;
    public string Decrypt(string ciphertext) => ciphertext;
    public string Hash(string value) => value;
}

internal sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}

internal static class TestDb
{
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static AppDbContext Create() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new PassThroughEncryptor());

    public static Account SeedAccount(AppDbContext db, Guid? userId = null, string bank = "TestBank")
    {
        var uid = userId ?? UserId;
        var connection = new BankConnection { Id = Guid.NewGuid(), UserId = uid, BankName = bank, Country = "BE", Provider = BankProvider.EnableBanking };
        var account = new Account { Id = Guid.NewGuid(), UserId = uid, BankConnectionId = connection.Id, BankConnection = connection, Identifier = "BE00", IdentifierHash = "h", DisplayName = "Main" };
        db.BankConnections.Add(connection);
        db.Accounts.Add(account);
        return account;
    }

    public static Transaction Tx(Account account, decimal amount, DateOnly date, int? categoryId = null, bool reimbursed = false,
        string counterparty = "Shop", string description = "", Guid? userId = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId ?? account.UserId,
        AccountId = account.Id,
        ExternalId = Guid.NewGuid().ToString(),
        Amount = amount,
        Date = date,
        CounterpartyName = counterparty,
        Description = description,
        CategoryId = categoryId,
        Reimbursed = reimbursed
    };

    public static Category Category(int id, CategoryKind kind = CategoryKind.Expense, bool active = true) =>
        new() { Id = id, Code = $"Cat{id}", Name = $"Category {id}", Kind = kind, IsActive = active };

    public static Group Group(Guid? userId = null, string name = "Trip") =>
        new() { Id = Guid.NewGuid(), UserId = userId ?? UserId, Name = name };
}
