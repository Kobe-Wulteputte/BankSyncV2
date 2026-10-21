namespace BS2.Domain.Entities;

/// <summary>
/// An authorization link started but not yet completed. The callback matches on <see cref="State"/>
/// instead of trusting any code posted at it.
/// </summary>
public class PendingAuthorization
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BankConnectionId { get; set; }
    public BankConnection BankConnection { get; set; } = null!;

    /// <summary>Opaque value round-tripped through the bank.</summary>
    public string State { get; set; } = string.Empty;

    [Encrypted]
    public string Url { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
