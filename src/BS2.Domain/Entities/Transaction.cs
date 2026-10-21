using BS2.Domain.Enums;

namespace BS2.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public TransactionSource Source { get; set; }

    /// <summary>Provider reference used for dedup. Unique per account.</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Signed: debit negative, credit positive.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    /// <summary>Effective date: the earlier of value and booking date (V1 rule).</summary>
    public DateOnly Date { get; set; }

    public DateOnly? BookingDate { get; set; }
    public DateOnly? ValueDate { get; set; }

    [Encrypted]
    public string CounterpartyName { get; set; } = string.Empty;

    [Encrypted]
    public string? CounterpartyIban { get; set; }

    [Encrypted]
    public string Description { get; set; } = string.Empty;

    /// <summary>Original provider payload, kept for future re-mapping.</summary>
    [Encrypted]
    public string? RawJson { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public ClassificationSource ClassificationSource { get; set; } = ClassificationSource.None;
    public DateTime? ClassifiedAt { get; set; }

    public bool Reimbursed { get; set; }

    [Encrypted]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<TransactionGroup> Groups { get; set; } = [];
    public ICollection<ClassificationRun> ClassificationRuns { get; set; } = [];
}
