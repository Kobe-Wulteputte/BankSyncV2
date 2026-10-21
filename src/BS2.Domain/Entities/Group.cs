namespace BS2.Domain.Entities;

/// <summary>Free-form user tag; a transaction can carry many.</summary>
public class Group
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<TransactionGroup> Transactions { get; set; } = [];
}

public class TransactionGroup
{
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
