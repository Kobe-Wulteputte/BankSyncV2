using BS2.Domain.Entities;

namespace BS2.Application.Abstractions;

/// <summary>
/// Provider-neutral view of an open-banking aggregator (Enable Banking today). Application code
/// never sees provider DTOs; Infrastructure maps them to these records.
/// </summary>
public interface IBankingProvider
{
    Task<IReadOnlyList<ProviderBank>> GetBanksAsync(string country, CancellationToken ct);

    /// <summary>Starts an authorization and returns the URL the user must visit. Throws <see cref="BankingProviderException"/> on failure.</summary>
    Task<string> StartAuthorizationAsync(BankConnection connection, string state, Uri redirectUrl, int consentValidityDays, CancellationToken ct);

    /// <summary>Exchanges the callback code for a session.</summary>
    Task<ProviderSession> CompleteAuthorizationAsync(string code, CancellationToken ct);

    /// <summary>Returns null when the session is unknown or dead.</summary>
    Task<ProviderSession?> GetSessionAsync(string sessionId, CancellationToken ct);

    Task DeleteSessionAsync(string sessionId, CancellationToken ct);

    Task<ProviderAccount?> GetAccountAsync(string accountId, CancellationToken ct);

    /// <summary>All transactions since <paramref name="from"/>, pagination handled internally.</summary>
    Task<IReadOnlyList<ProviderTransaction>> GetTransactionsAsync(string accountId, DateOnly from, CancellationToken ct);
}

public sealed record ProviderBank(
    string Name,
    string Country,
    string? LogoUrl,
    IReadOnlyList<string> PsuTypes,
    int? MaxConsentValidityDays,
    bool? SupportsAccountPreselection);

public sealed record ProviderSession(
    string SessionId,
    string BankName,
    string Country,
    DateTime? ValidUntil,
    IReadOnlyList<ProviderAccount> Accounts);

/// <param name="Identifier">Normalized IBAN, or a provider label when the account has none (PayPal).</param>
public sealed record ProviderAccount(
    string AccountId,
    string Identifier,
    bool IsIban,
    string? Name,
    string Currency);

public sealed record ProviderTransaction(
    string ExternalId,
    decimal Amount,
    string Currency,
    DateOnly? BookingDate,
    DateOnly? ValueDate,
    string CounterpartyName,
    string? CounterpartyIban,
    string Description,
    string RawJson);

public sealed class BankingProviderException(string message, Exception? inner = null) : Exception(message, inner);
