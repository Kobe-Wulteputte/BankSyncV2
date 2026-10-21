using BS2.Application;
using BS2.Application.Abstractions;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using BS2.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BS2.Tests.Banking;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);
}

/// <summary>Pass-through encryption with a deterministic, recognisable hash.</summary>
public sealed class FakeEncryptor : IFieldEncryptor
{
    public string Encrypt(string plaintext) => plaintext;
    public string Decrypt(string ciphertext) => ciphertext;
    public string Hash(string value) => "h:" + value;
}

public sealed class FakeBankingProvider : IBankingProvider
{
    public ProviderSession? CompleteResult { get; set; }
    public ProviderSession? SessionResult { get; set; }
    public Dictionary<string, List<ProviderTransaction>> Transactions { get; } = new();
    public List<string> StartedStates { get; } = [];
    public List<string> DeletedSessions { get; } = [];
    public List<string> FetchedAccounts { get; } = [];
    public int CompleteCalls { get; private set; }
    public Exception? CompleteError { get; set; }
    public Exception? TransactionsError { get; set; }

    public Task<IReadOnlyList<ProviderBank>> GetBanksAsync(string country, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProviderBank>>([]);

    public Task<string> StartAuthorizationAsync(BankConnection connection, string state, Uri redirectUrl, int consentValidityDays, CancellationToken ct)
    {
        StartedStates.Add(state);
        return Task.FromResult($"https://bank.example/auth?state={state}&redirect={redirectUrl}");
    }

    public Task<ProviderSession> CompleteAuthorizationAsync(string code, CancellationToken ct)
    {
        CompleteCalls++;
        if (CompleteError is not null) throw CompleteError;
        return Task.FromResult(CompleteResult ?? throw new BankingProviderException("no session configured"));
    }

    public Task<ProviderSession?> GetSessionAsync(string sessionId, CancellationToken ct) => Task.FromResult(SessionResult);

    public Task DeleteSessionAsync(string sessionId, CancellationToken ct)
    {
        DeletedSessions.Add(sessionId);
        return Task.CompletedTask;
    }

    public Task<ProviderAccount?> GetAccountAsync(string accountId, CancellationToken ct) => Task.FromResult<ProviderAccount?>(null);

    public Task<IReadOnlyList<ProviderTransaction>> GetTransactionsAsync(string accountId, DateOnly from, CancellationToken ct)
    {
        FetchedAccounts.Add(accountId);
        if (TransactionsError is not null) throw TransactionsError;
        return Task.FromResult<IReadOnlyList<ProviderTransaction>>(Transactions.GetValueOrDefault(accountId) ?? []);
    }
}

public sealed class FakeClassifier : ITransactionClassificationService
{
    public List<Guid> Classified { get; } = [];

    public Task<ClassificationRun> ClassifyAsync(Transaction transaction, ClassificationTrigger trigger, bool force, CancellationToken ct)
    {
        Classified.Add(transaction.Id);
        return Task.FromResult(new ClassificationRun { Id = Guid.NewGuid(), TransactionId = transaction.Id, Trigger = trigger, Accepted = true });
    }
}

public sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Html, string? Text)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string htmlBody, string? plainTextBody, CancellationToken ct = default)
    {
        Sent.Add((to, subject, htmlBody, plainTextBody));
        return Task.CompletedTask;
    }
}

public static class TestEnv
{
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static AppDbContext NewDb(FakeEncryptor encryptor) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, encryptor);

    public static IOptions<SyncOptions> SyncOptions(Action<SyncOptions>? configure = null)
    {
        var o = new SyncOptions();
        configure?.Invoke(o);
        return Options.Create(o);
    }

    public static IOptions<AppOptions> AppOptions() => Options.Create(new AppOptions { PublicOrigin = "https://localhost:8080" });

    public static BankConnection NewConnection(FakeClock clock, string bankName = "Revolut", ConnectionStatus status = ConnectionStatus.Active,
        string? sessionId = "sess-old", DateTime? validUntil = null, params string[] ibans) => new()
    {
        Id = Guid.NewGuid(),
        UserId = UserId,
        Provider = BankProvider.EnableBanking,
        BankName = bankName,
        Country = "BE",
        Status = status,
        ExternalSessionId = sessionId,
        ValidUntil = validUntil ?? (status == ConnectionStatus.Active ? clock.UtcNow.AddDays(30) : null),
        ConfiguredIbans = ibans,
        CreatedAt = clock.UtcNow,
        UpdatedAt = clock.UtcNow
    };

    public static Account NewAccount(BankConnection connection, string identifier, string externalId, FakeEncryptor encryptor, FakeClock clock) => new()
    {
        Id = Guid.NewGuid(),
        UserId = connection.UserId,
        BankConnectionId = connection.Id,
        Identifier = identifier,
        IdentifierHash = encryptor.Hash(identifier),
        ExternalAccountId = externalId,
        Currency = "EUR",
        CreatedAt = clock.UtcNow,
        UpdatedAt = clock.UtcNow
    };

    public static ProviderTransaction Tx(string externalId, decimal amount = -12.5m) =>
        new(externalId, amount, "EUR", new DateOnly(2026, 10, 1), new DateOnly(2026, 9, 30), "Shop", null, "Groceries", "{}");
}
