namespace BS2.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    /// <summary>Auth0 <c>sub</c> claim.</summary>
    public string Auth0Subject { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
