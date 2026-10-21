using BS2.Domain.Enums;

namespace BS2.Domain.Entities;

/// <summary>
/// Categories are global, not per user: the fine-tuned model was trained on these exact codes.
/// <see cref="Code"/> is what the model emits/was prompted with ("FoodAndDrink");
/// <see cref="Name"/> is the display label ("Food and drink (other)").
/// </summary>
public class Category
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Color { get; set; }
    public int? CategoryClassId { get; set; }
    public CategoryClass? CategoryClass { get; set; }
}
