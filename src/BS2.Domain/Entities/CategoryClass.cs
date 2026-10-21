namespace BS2.Domain.Entities;

/// <summary>Groups categories for reporting (Food, Housing, ...). Global, like categories.</summary>
public class CategoryClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public ICollection<Category> Categories { get; set; } = [];
}
