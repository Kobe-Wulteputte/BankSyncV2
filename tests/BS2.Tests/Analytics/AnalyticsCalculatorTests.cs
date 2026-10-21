using BS2.Application.Analytics;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Xunit;

namespace BS2.Tests.Analytics;

public class AnalyticsCalculatorTests
{
    private static readonly Category Food = new() { Id = 1, Code = "Food", Name = "Food", Kind = CategoryKind.Expense, Color = "#f00" };
    private static readonly Category Rent = new() { Id = 2, Code = "Rent", Name = "Rent", Kind = CategoryKind.Expense };
    private static readonly Category Salary = new() { Id = 3, Code = "Salary", Name = "Salary", Kind = CategoryKind.Income };
    private static readonly Category Transfer = new() { Id = 4, Code = "Transfer", Name = "Transfer", Kind = CategoryKind.Transfer };
    private static readonly Category[] Categories = [Food, Rent, Salary, Transfer];

    private static AnalyticsRow Row(string date, decimal amount, Category? cat, bool reimbursed = false) =>
        new(DateOnly.Parse(date), amount, cat?.Id, cat?.Kind, reimbursed);

    private static readonly AnalyticsRow[] Sample =
    [
        Row("2026-01-05", -100, Food),
        Row("2026-01-06", -300, Rent),
        Row("2026-01-07", -50, null),             // uncategorized expense by sign
        Row("2026-01-08", 25, null),              // uncategorized income by sign
        Row("2026-01-10", 2000, Salary),
        Row("2026-01-11", -500, Transfer),        // never counted
        Row("2026-01-12", 500, Transfer),
        Row("2026-03-02", -100, Food, reimbursed: true),
        Row("2026-03-03", 1000, Salary),
        Row("2026-03-04", 40, Food)               // positive amount in an Expense category counts as income by sign
    ];

    [Fact]
    public void Expenses_excludes_transfers_returns_positive_and_sorts_desc()
    {
        var result = AnalyticsCalculator.Expenses(Sample, Categories);

        Assert.Equal(550m, result.Total);
        Assert.Equal(4, result.Count);
        Assert.Equal(["Rent", "Food", "Uncategorized"], result.Categories.Select(c => c.Code));
        Assert.Equal([300m, 200m, 50m], result.Categories.Select(c => c.Total));
        Assert.Equal("#f00", result.Categories[1].Color);
    }

    [Fact]
    public void Expenses_shares_sum_to_one()
    {
        var result = AnalyticsCalculator.Expenses(Sample, Categories);
        Assert.Equal(1m, result.Categories.Sum(c => c.Share), 10);
        Assert.Equal(300m / 550m, result.Categories[0].Share);
    }

    [Fact]
    public void Expenses_uncategorized_bucket_has_null_id()
    {
        var bucket = AnalyticsCalculator.Expenses(Sample, Categories).Categories.Single(c => c.CategoryId is null);
        Assert.Equal("Uncategorized", bucket.Code);
        Assert.Equal("Uncategorized", bucket.Name);
        Assert.Equal(50m, bucket.Total);
    }

    [Fact]
    public void Expenses_includes_reimbursed_rows()
    {
        var food = AnalyticsCalculator.Expenses(Sample, Categories).Categories.Single(c => c.CategoryId == Food.Id);
        Assert.Equal(200m, food.Total);
        Assert.Equal(2, food.Count);
    }

    [Fact]
    public void Expenses_empty_input()
    {
        var result = AnalyticsCalculator.Expenses([], Categories);
        Assert.Equal(0m, result.Total);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public void Income_zero_fills_months_between_bounds()
    {
        var result = AnalyticsCalculator.Income(Sample, new DateOnly(2025, 12, 1), new DateOnly(2026, 4, 30), Categories);

        Assert.Equal(["2025-12", "2026-01", "2026-02", "2026-03", "2026-04"], result.Months.Select(m => m.Month));
        Assert.Equal([0m, 2025m, 0m, 1040m, 0m], result.Months.Select(m => m.Total));
        Assert.Equal(3065m, result.Total);
    }

    [Fact]
    public void Income_uses_row_bounds_when_from_to_are_null()
    {
        var result = AnalyticsCalculator.Income(Sample, null, null, Categories);
        Assert.Equal(["2026-01", "2026-02", "2026-03"], result.Months.Select(m => m.Month));
    }

    [Fact]
    public void Income_by_category_includes_uncategorized_and_excludes_transfers()
    {
        var jan = AnalyticsCalculator.Income(Sample, null, null, Categories).Months[0];
        Assert.Equal(["Salary", "Uncategorized"], jan.ByCategory.Select(c => c.Code));
        Assert.Equal([2000m, 25m], jan.ByCategory.Select(c => c.Total));
    }

    [Fact]
    public void Income_empty_input_gives_empty_months_unless_bounds_given()
    {
        Assert.Empty(AnalyticsCalculator.Income([], null, null, Categories).Months);
        Assert.Empty(AnalyticsCalculator.Income([], new DateOnly(2026, 1, 1), null, Categories).Months);
        Assert.Equal(2, AnalyticsCalculator.Income([], new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), Categories).Months.Length);
    }

    [Fact]
    public void Savings_is_income_minus_expenses_with_running_total()
    {
        var result = AnalyticsCalculator.Savings(Sample, null, null);

        Assert.Equal(3065m, result.TotalIncome);
        Assert.Equal(550m, result.TotalExpenses);
        Assert.Equal(2515m, result.TotalSavings);

        Assert.Equal(["2026-01", "2026-02", "2026-03"], result.Months.Select(m => m.Month));
        var jan = result.Months[0];
        Assert.Equal((2025m, 450m, 1575m, 1575m), (jan.Income, jan.Expenses, jan.Savings, jan.Cumulative));
        var feb = result.Months[1];
        Assert.Equal((0m, 0m, 0m, 1575m), (feb.Income, feb.Expenses, feb.Savings, feb.Cumulative));
        var mar = result.Months[2];
        Assert.Equal((1040m, 100m, 940m, 2515m), (mar.Income, mar.Expenses, mar.Savings, mar.Cumulative));
    }

    [Fact]
    public void Savings_ignores_transfers_entirely()
    {
        var onlyTransfers = new[] { Row("2026-01-01", -500, Transfer), Row("2026-01-01", 500, Transfer) };
        var result = AnalyticsCalculator.Savings(onlyTransfers, null, null);
        Assert.Equal(0m, result.TotalIncome);
        Assert.Equal(0m, result.TotalExpenses);
        Assert.Empty(result.Months);
        Assert.Equal(2, AnalyticsCalculator.Savings(onlyTransfers, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1)).Months.Length);
    }

    [Fact]
    public void MonthKey_is_zero_padded() =>
        Assert.Equal("2026-03", AnalyticsCalculator.MonthKey(new DateOnly(2026, 3, 31)));

    private static readonly CategoryClass Needs = new() { Id = 10, Name = "Needs", Color = "#00f" };
    private static readonly Category Bills = new() { Id = 11, Code = "Bills", Name = "Bills", Kind = CategoryKind.Expense, CategoryClassId = 10, CategoryClass = Needs };
    private static readonly Category Pay = new() { Id = 12, Code = "Pay", Name = "Pay", Kind = CategoryKind.Income, CategoryClassId = 10, CategoryClass = Needs };
    private static readonly Category[] Classed = [Food, Rent, Bills, Pay];
    private static readonly Category Groceries = new() { Id = 13, Code = "Groceries", Name = "Groceries", Kind = CategoryKind.Expense, CategoryClassId = 10, CategoryClass = Needs };

    [Fact]
    public void Expenses_groups_by_class_with_unclassed_bucket_and_shares()
    {
        var rows = new[] { Row("2026-01-01", -100, Bills), Row("2026-01-02", -50, Groceries), Row("2026-01-03", -50, Food), Row("2026-01-04", -100, null) };
        var result = AnalyticsCalculator.Expenses(rows, [.. Classed, Groceries]);

        Assert.Equal(2, result.Classes.Length);
        var needs = result.Classes[0];
        Assert.Equal((10, "Needs", "#00f", 150m, 2), (needs.CategoryClassId, needs.Name, needs.Color, needs.Total, needs.Count));
        var unclassed = result.Classes[1];
        Assert.Equal((null, "Unclassed", null, 150m), (unclassed.CategoryClassId, unclassed.Name, unclassed.Color, unclassed.Total));
        Assert.Equal(1m, result.Classes.Sum(c => c.Share), 10);
        Assert.Equal("Needs", result.Categories.First(c => c.CategoryId == 11).CategoryClassName);
    }

    [Fact]
    public void Income_by_class_per_month()
    {
        var rows = new[] { Row("2026-01-05", 100, Pay), Row("2026-01-06", 20, null), Row("2026-02-05", 300, Pay) };
        var months = AnalyticsCalculator.Income(rows, null, null, Classed).Months;

        Assert.Equal([("Needs", 100m), ("Unclassed", 20m)], months[0].ByClass.Select(c => (c.Name, c.Total)));
        Assert.Equal([("Needs", 300m)], months[1].ByClass.Select(c => (c.Name, c.Total)));
        Assert.Equal(10, months[0].ByCategory[0].CategoryClassId);
    }
}
