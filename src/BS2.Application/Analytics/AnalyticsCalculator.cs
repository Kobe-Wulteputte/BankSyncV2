using BS2.Application.Common;
using BS2.Domain.Entities;
using BS2.Domain.Enums;

namespace BS2.Application.Analytics;

public readonly record struct AnalyticsRow(DateOnly Date, decimal Amount, int? CategoryId, CategoryKind? Kind, bool Reimbursed);

public sealed record ExpensesResult(decimal Total, int Count, ExpenseCategoryDto[] Categories, ExpenseClassDto[] Classes);
public sealed record ExpenseCategoryDto(int? CategoryId, string Code, string Name, string? Color, decimal Total, int Count, decimal Share, int? CategoryClassId, string? CategoryClassName);
public sealed record ExpenseClassDto(int? CategoryClassId, string Name, string? Color, decimal Total, int Count, decimal Share);

public sealed record IncomeResult(decimal Total, IncomeMonthDto[] Months);
public sealed record IncomeMonthDto(string Month, decimal Total, IncomeCategoryDto[] ByCategory, IncomeClassDto[] ByClass);
public sealed record IncomeCategoryDto(int? CategoryId, string Code, string Name, decimal Total, int? CategoryClassId);
public sealed record IncomeClassDto(int? CategoryClassId, string Name, decimal Total);

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
        var buckets = rows
            .Where(r => TransactionRules.IsExpense(r.Amount, r.Kind))
            .GroupBy(r => r.CategoryId)
            .Select(g => (CategoryId: g.Key, Total: -g.Sum(r => r.Amount), Count: g.Count()))
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

    public static IncomeResult Income(IEnumerable<AnalyticsRow> rows, DateOnly? from, DateOnly? to, IEnumerable<Category> categories)
    {
        var lookup = categories.ToDictionary(c => c.Id);
        var income = rows.Where(r => TransactionRules.IsIncome(r.Amount, r.Kind)).ToList();
        var byMonth = income.ToLookup(r => MonthKey(r.Date));

        var months = Months(income, from, to).Select(m =>
        {
            var inMonth = byMonth[m];
            var byCategory = inMonth
                .GroupBy(r => r.CategoryId)
                .Select(g =>
                {
                    var cat = g.Key is { } id ? lookup.GetValueOrDefault(id) : null;
                    return new IncomeCategoryDto(g.Key, cat?.Code ?? Uncategorized, cat?.Name ?? Uncategorized, g.Sum(r => r.Amount), cat?.CategoryClassId);
                })
                .OrderByDescending(c => c.Total)
                .ToArray();
            var byClass = byCategory
                .GroupBy(c => c.CategoryClassId)
                .Select(g => new IncomeClassDto(
                    g.Key,
                    g.Key is null ? Unclassed : lookup[g.First().CategoryId!.Value].CategoryClass?.Name ?? Unclassed,
                    g.Sum(c => c.Total)))
                .OrderByDescending(c => c.Total)
                .ToArray();
            return new IncomeMonthDto(m, inMonth.Sum(r => r.Amount), byCategory, byClass);
        }).ToArray();

        return new IncomeResult(income.Sum(r => r.Amount), months);
    }

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
