using BS2.Domain.Entities;
using BS2.Domain.Enums;

namespace BS2.Application.Analytics;

public readonly record struct AnalyticsRow(DateOnly Date, decimal Amount, int? CategoryId, CategoryKind? Kind, bool Reimbursed);

public sealed record ExpensesResult(decimal Total, int Count, ExpenseCategoryDto[] Categories, ExpenseClassDto[] Classes);
public sealed record ExpenseCategoryDto(int? CategoryId, string Code, string Name, string? Color, decimal Total, int Count, decimal Share, int? CategoryClassId, string? CategoryClassName);
public sealed record ExpenseClassDto(int? CategoryClassId, string Name, string? Color, decimal Total, int Count, decimal Share);

public sealed record MonthlyResult(decimal Total, MonthlyMonthDto[] Months);
public sealed record MonthlyMonthDto(string Month, decimal Total, MonthlyCategoryDto[] ByCategory, MonthlyClassDto[] ByClass);
public sealed record MonthlyCategoryDto(int? CategoryId, string Code, string Name, decimal Total, int? CategoryClassId);
public sealed record MonthlyClassDto(int? CategoryClassId, string Name, decimal Total);

public sealed record SavingsResult(decimal TotalIncome, decimal TotalExpenses, decimal TotalSavings, SavingsMonthDto[] Months);
public sealed record SavingsMonthDto(string Month, decimal Income, decimal Expenses, decimal Savings, decimal Cumulative);

/// <summary>Pure aggregation over already-filtered rows. Transfers never count; expenses come out positive.</summary>
public static class AnalyticsCalculator
{
    private const string Uncategorized = "Uncategorized";
    private const string Unclassed = "Unclassed";

    public static ExpensesResult Expenses(IEnumerable<AnalyticsRow> rows, IEnumerable<Category> categories)
    {
        var lookup = categories.ToDictionary(c => c.Id);
        var buckets = NetByCategory(rows)
            .Where(b => b.Net < 0)
            .Select(b => (b.CategoryId, Total: -b.Net, b.Count))
            .OrderByDescending(b => b.Total)
            .ToList();

        var grand = buckets.Sum(b => b.Total);
        var result = buckets.Select(b =>
        {
            var cat = b.CategoryId is { } id ? lookup.GetValueOrDefault(id) : null;
            return new ExpenseCategoryDto(
                b.CategoryId, cat?.Code ?? Uncategorized, cat?.Name ?? Uncategorized, cat?.Color,
                b.Total, b.Count, grand == 0 ? 0 : b.Total / grand, cat?.CategoryClassId, cat?.CategoryClass?.Name);
        }).ToArray();

        var classes = result
            .GroupBy(c => c.CategoryClassId)
            .Select(g =>
            {
                var cls = g.Key is null ? null : lookup[g.First().CategoryId!.Value].CategoryClass;
                var total = g.Sum(c => c.Total);
                return new ExpenseClassDto(g.Key, cls?.Name ?? Unclassed, cls?.Color, total, g.Sum(c => c.Count), grand == 0 ? 0 : total / grand);
            })
            .OrderByDescending(c => c.Total)
            .ToArray();

        return new ExpensesResult(grand, buckets.Sum(b => b.Count), result, classes);
    }

    public static MonthlyResult Income(IEnumerable<AnalyticsRow> rows, DateOnly? from, DateOnly? to, IEnumerable<Category> categories) =>
        Monthly(rows, from, to, categories, sign: 1);

    /// <summary>Expenses per month and category, as positive amounts; same netting rule as <see cref="Expenses"/>.</summary>
    public static MonthlyResult MonthlyExpenses(IEnumerable<AnalyticsRow> rows, DateOnly? from, DateOnly? to, IEnumerable<Category> categories) =>
        Monthly(rows, from, to, categories, sign: -1);

    /// <summary>Per month, keeps the categories whose sum has <paramref name="sign"/> and reports it as a positive total.</summary>
    private static MonthlyResult Monthly(IEnumerable<AnalyticsRow> rows, DateOnly? from, DateOnly? to, IEnumerable<Category> categories, int sign)
    {
        var lookup = categories.ToDictionary(c => c.Id);
        var relevant = rows.Where(r => r.Kind != CategoryKind.Transfer && r.Amount != 0).ToList();
        var byMonth = relevant.ToLookup(r => MonthKey(r.Date));

        var months = Months(relevant.Where(r => Math.Sign(r.Amount) == sign).ToList(), from, to).Select(m =>
        {
            var byCategory = NetByCategory(byMonth[m])
                .Where(b => Math.Sign(b.Net) == sign)
                .Select(b =>
                {
                    var cat = b.CategoryId is { } id ? lookup.GetValueOrDefault(id) : null;
                    return new MonthlyCategoryDto(b.CategoryId, cat?.Code ?? Uncategorized, cat?.Name ?? Uncategorized, b.Net * sign, cat?.CategoryClassId);
                })
                .OrderByDescending(c => c.Total)
                .ToArray();
            var byClass = byCategory
                .GroupBy(c => c.CategoryClassId)
                .Select(g => new MonthlyClassDto(
                    g.Key,
                    g.Key is null ? Unclassed : lookup[g.First().CategoryId!.Value].CategoryClass?.Name ?? Unclassed,
                    g.Sum(c => c.Total)))
                .OrderByDescending(c => c.Total)
                .ToArray();
            return new MonthlyMonthDto(m, byCategory.Sum(c => c.Total), byCategory, byClass);
        }).ToArray();

        return new MonthlyResult(months.Sum(m => m.Total), months);
    }

    /// <summary>
    /// Sums every non-transfer row per category, so a refund lowers its category's expense and a
    /// category lands on the expense or income side by the sign of its sum. Uncategorized rows stay
    /// split by sign: they are unrelated entries, and netting them would hide both sides.
    /// </summary>
    private static IEnumerable<(int? CategoryId, decimal Net, int Count)> NetByCategory(IEnumerable<AnalyticsRow> rows) =>
        rows.Where(r => r.Kind != CategoryKind.Transfer && r.Amount != 0)
            .GroupBy(r => (r.CategoryId, UncategorizedIncome: r.CategoryId is null && r.Amount > 0))
            .Select(g => (g.Key.CategoryId, g.Sum(r => r.Amount), g.Count()));

    public static SavingsResult Savings(IEnumerable<AnalyticsRow> rows, DateOnly? from, DateOnly? to)
    {
        var relevant = rows.Where(r => r.Kind != CategoryKind.Transfer && r.Amount != 0).ToList();
        var byMonth = relevant.ToLookup(r => MonthKey(r.Date));

        var cumulative = 0m;
        var months = Months(relevant, from, to).Select(m =>
        {
            var inMonth = byMonth[m].ToList();
            var inc = inMonth.Where(r => r.Amount > 0).Sum(r => r.Amount);
            var exp = -inMonth.Where(r => r.Amount < 0).Sum(r => r.Amount);
            cumulative += inc - exp;
            return new SavingsMonthDto(m, inc, exp, inc - exp, cumulative);
        }).ToArray();

        var totalIncome = months.Sum(m => m.Income);
        var totalExpenses = months.Sum(m => m.Expenses);
        return new SavingsResult(totalIncome, totalExpenses, totalIncome - totalExpenses, months);
    }

    public static string MonthKey(DateOnly d) => $"{d.Year:D4}-{d.Month:D2}";

    /// <summary>Contiguous month keys from <paramref name="from"/> to <paramref name="to"/>; row bounds fill in whichever is null.</summary>
    private static IEnumerable<string> Months(IReadOnlyCollection<AnalyticsRow> rows, DateOnly? from, DateOnly? to)
    {
        var start = from ?? (rows.Count == 0 ? null : rows.Min(r => r.Date));
        var end = to ?? (rows.Count == 0 ? null : rows.Max(r => r.Date));
        if (start is null || end is null) yield break;

        for (var m = new DateOnly(start.Value.Year, start.Value.Month, 1); m <= end.Value; m = m.AddMonths(1))
            yield return MonthKey(m);
    }
}
