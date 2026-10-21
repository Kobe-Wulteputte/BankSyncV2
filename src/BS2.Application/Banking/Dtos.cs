using BS2.Domain.Enums;

namespace BS2.Application.Banking;

public sealed record AccountDto(
    Guid Id,
    Guid BankConnectionId,
    string BankName,
    string Identifier,
    string? DisplayName,
    string Currency,
    bool IsActive,
    int TransactionCount);

public sealed record PendingAuthorizationDto(string Url, DateTime CreatedAt, DateTime ExpiresAt);

public sealed record BankConnectionDto(
    Guid Id,
    BankProvider Provider,
    string BankName,
    string Country,
    string PsuType,
    bool SelectAccountsAtBank,
    int? ConsentValidityDays,
    IReadOnlyList<string> ConfiguredIbans,
    ConnectionStatus Status,
    DateTime? ValidUntil,
    DateTime? LastAuthorizedAt,
    DateTime? LastSyncedAt,
    string? LastSyncError,
    IReadOnlyList<AccountDto> Accounts,
    PendingAuthorizationDto? PendingAuthorization);

public sealed record UpsertBankConnectionRequest(
    string BankName,
    string Country,
    string? PsuType,
    bool? SelectAccountsAtBank,
    int? ConsentValidityDays,
    IReadOnlyList<string>? Ibans);

/// <summary><see cref="DisplayName"/>: null keeps the current value, an empty string clears it.</summary>
public sealed record UpdateAccountRequest(string? DisplayName, bool IsActive);

public sealed record StartAuthorizationResult(string Url, string State, DateTime ExpiresAt);

public sealed record CompleteAuthorizationRequest(string? Code, string? State);

/// <summary>Outcome of completing an authorization. <see cref="UserId"/> is null when the state was unknown.</summary>
public sealed record AuthorizationResult(bool Success, string BankName, string Message, Guid? UserId);
