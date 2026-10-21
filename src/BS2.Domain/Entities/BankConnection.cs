using BS2.Domain.Enums;

namespace BS2.Domain.Entities;

/// <summary>
/// One configured bank for one user, plus whatever provider session currently backs it.
/// Replaces V1's appsettings <c>EnableBanking:Banks[]</c> entry and its <c>session-keys.json</c> record.
/// </summary>
public class BankConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public BankProvider Provider { get; set; }

    /// <summary>ASPSP name exactly as the provider lists it, e.g. "Revolut".</summary>
    public string BankName { get; set; } = string.Empty;

    /// <summary>Two-letter country code, e.g. "BE".</summary>
    public string Country { get; set; } = string.Empty;

    public string PsuType { get; set; } = "personal";

    /// <summary>
    /// Set for ASPSPs that will not honour a pre-specified account list; the user picks accounts in
    /// the bank's own screens and <see cref="ConfiguredIbans"/> acts purely as a sync filter.
    /// </summary>
    public bool SelectAccountsAtBank { get; set; }

    /// <summary>Overrides the global consent length. ASPSPs cap this differently.</summary>
    public int? ConsentValidityDays { get; set; }

    /// <summary>
    /// Normalized IBANs to sync, stored as one encrypted comma-separated string.
    /// Empty means sync every account the session exposes.
    /// </summary>
    [Encrypted]
    public string ConfiguredIbansRaw { get; set; } = string.Empty;

    /// <summary>Provider session id (Enable Banking session UUID). Null until first authorization.</summary>
    [Encrypted]
    public string? ExternalSessionId { get; set; }

    public ConnectionStatus Status { get; set; } = ConnectionStatus.NotAuthorized;
    public DateTime? ValidUntil { get; set; }
    public DateTime? LastAuthorizedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Account> Accounts { get; set; } = [];

    public IReadOnlyList<string> ConfiguredIbans
    {
        get => ConfiguredIbansRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        set => ConfiguredIbansRaw = string.Join(',', value);
    }

    /// <summary>True when no IBANs are listed, meaning every account the session exposes is synced.</summary>
    public bool SyncsAllAccounts => ConfiguredIbans.Count == 0;
}
