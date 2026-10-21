namespace BS2.Domain.Entities;

/// <summary>
/// A bank account identified by IBAN (or a provider label when the ASPSP has no IBANs, e.g.
/// PayPal). Stable across re-authorizations: a new session re-points the same Account.
/// </summary>
public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BankConnectionId { get; set; }
    public BankConnection BankConnection { get; set; } = null!;

    /// <summary>Provider account id (Enable Banking account UID).</summary>
    [Encrypted]
    public string? ExternalAccountId { get; set; }

    /// <summary>Normalized IBAN or provider label.</summary>
    [Encrypted]
    public string Identifier { get; set; } = string.Empty;

    /// <summary>HMAC of <see cref="Identifier"/> so the row can be found without decrypting every account.</summary>
    public string IdentifierHash { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
    public string Currency { get; set; } = "EUR";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = [];
}
