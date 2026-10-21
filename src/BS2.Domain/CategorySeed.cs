using BS2.Domain.Entities;
using BS2.Domain.Enums;

namespace BS2.Domain;

/// <summary>
/// The V1 <c>CategoryEnum</c>, frozen. Codes are the enum member names the fine-tuned model was
/// prompted with; names are the <c>EnumMember</c> display values written to the workbook.
/// Ids are fixed so migrations and tests can reference them.
/// </summary>
public static class CategorySeed
{
    public static readonly IReadOnlyList<CategoryClass> Classes =
    [
        new() { Id = 1, Name = "Income", Color = "#59a14f" },
        new() { Id = 2, Name = "Housing", Color = "#4e79a7" },
        new() { Id = 3, Name = "Cost of living", Color = "#f28e2b" },
        new() { Id = 4, Name = "Food", Color = "#e15759" },
        new() { Id = 5, Name = "Fun", Color = "#b07aa1" },
        new() { Id = 6, Name = "Social", Color = "#edc948" },
        new() { Id = 7, Name = "Sports", Color = "#76b7b2" },
        new() { Id = 8, Name = "Unknown", Color = "#9aa0a6" }
    ];

    public static readonly IReadOnlyList<Category> Categories =
    [
        new() { Id = 1, Code = "Clothes", Name = "Clothes", CategoryClassId = 3, Kind = CategoryKind.Expense },
        new() { Id = 2, Code = "Communication", Name = "Communication", CategoryClassId = 3, Kind = CategoryKind.Expense },
        new() { Id = 3, Code = "Education", Name = "Education", CategoryClassId = 3, Kind = CategoryKind.Expense },
        new() { Id = 4, Code = "FoodAndDrink", Name = "Food and drink (other)", CategoryClassId = 4, Kind = CategoryKind.Expense },
        new() { Id = 5, Code = "Groceries", Name = "Groceries", CategoryClassId = 4, Kind = CategoryKind.Expense },
        new() { Id = 6, Code = "Health", Name = "Health", CategoryClassId = 3, Kind = CategoryKind.Expense },
        new() { Id = 7, Code = "Home", Name = "Home", CategoryClassId = 2, Kind = CategoryKind.Expense },
        new() { Id = 8, Code = "Activities", Name = "Activities", CategoryClassId = 5, Kind = CategoryKind.Expense },
        new() { Id = 9, Code = "Chiro", Name = "Chiro", CategoryClassId = 6, Kind = CategoryKind.Expense },
        new() { Id = 10, Code = "Cycling", Name = "Cycling", CategoryClassId = 7, Kind = CategoryKind.Expense },
        new() { Id = 11, Code = "Sports", Name = "Sports", CategoryClassId = 7, Kind = CategoryKind.Expense },
        new() { Id = 12, Code = "Subscriptions", Name = "Subscriptions", CategoryClassId = 5, Kind = CategoryKind.Expense },
        new() { Id = 13, Code = "Takeaway", Name = "Takeaway", CategoryClassId = 4, Kind = CategoryKind.Expense },
        new() { Id = 14, Code = "Transport", Name = "Transport", CategoryClassId = 3, Kind = CategoryKind.Expense },
        new() { Id = 15, Code = "FastFood", Name = "Fast food", CategoryClassId = 4, Kind = CategoryKind.Expense },
        new() { Id = 16, Code = "Gifts", Name = "Gifts", CategoryClassId = 5, Kind = CategoryKind.Expense },
        new() { Id = 17, Code = "Donations", Name = "Donations", CategoryClassId = 1, Kind = CategoryKind.Expense },
        new() { Id = 18, Code = "Drinks", Name = "Drinks", CategoryClassId = 6, Kind = CategoryKind.Expense },
        new() { Id = 19, Code = "Gadgets", Name = "Gadgets", CategoryClassId = 5, Kind = CategoryKind.Expense },
        new() { Id = 20, Code = "Games", Name = "Games", CategoryClassId = 5, Kind = CategoryKind.Expense },
        new() { Id = 21, Code = "Restaurants", Name = "Restaurants", CategoryClassId = 4, Kind = CategoryKind.Expense },
        new() { Id = 22, Code = "Shows", Name = "Shows", CategoryClassId = 6, Kind = CategoryKind.Expense },
        new() { Id = 23, Code = "Travel", Name = "Travel", CategoryClassId = 5, Kind = CategoryKind.Expense },
        new() { Id = 24, Code = "BankServices", Name = "Bank services", CategoryClassId = 3, Kind = CategoryKind.Expense },
        new() { Id = 25, Code = "Fines", Name = "Fines", CategoryClassId = 3, Kind = CategoryKind.Expense },
        new() { Id = 26, Code = "Cash", Name = "Cash", CategoryClassId = 8, Kind = CategoryKind.Expense },
        new() { Id = 27, Code = "Investments", Name = "Investments", CategoryClassId = 1, Kind = CategoryKind.Transfer },
        new() { Id = 28, Code = "Transfer", Name = "Transfer", Kind = CategoryKind.Transfer },
        new() { Id = 29, Code = "Vouchers", Name = "Vouchers", CategoryClassId = 1, Kind = CategoryKind.Income },
        new() { Id = 30, Code = "Bonus", Name = "Bonus", CategoryClassId = 1, Kind = CategoryKind.Income },
        new() { Id = 31, Code = "Wage", Name = "Wage", CategoryClassId = 1, Kind = CategoryKind.Income },
        new() { Id = 32, Code = "Taxes", Name = "Taxes", CategoryClassId = 1, Kind = CategoryKind.Expense },
        new() { Id = 33, Code = "Reimbursement", Name = "Reimbursement", CategoryClassId = 1, Kind = CategoryKind.Income },
        new() { Id = 34, Code = "Utilities", Name = "Utilities", CategoryClassId = 2, Kind = CategoryKind.Expense },
        new() { Id = 35, Code = "Mortgage", Name = "Mortgage", CategoryClassId = 2, Kind = CategoryKind.Expense }
    ];

    static CategorySeed()
    {
        for (var i = 0; i < Classes.Count; i++) Classes[i].SortOrder = i;
        for (var i = 0; i < Categories.Count; i++) Categories[i].SortOrder = i;
    }

    /// <summary>
    /// Resolves a model/workbook string to a category: exact code, exact display name, or a
    /// whitespace/punctuation-insensitive match on either. Null when nothing matches.
    /// </summary>
    public static Category? Resolve(string? raw, IEnumerable<Category>? categories = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var list = categories ?? Categories;
        var trimmed = raw.Trim();

        return list.FirstOrDefault(c => string.Equals(c.Code, trimmed, StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault(c => string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault(c => Loose(c.Code) == Loose(trimmed) || Loose(c.Name) == Loose(trimmed));
    }

    private static string Loose(string s) =>
        new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
