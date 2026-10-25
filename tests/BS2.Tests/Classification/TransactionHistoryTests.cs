using BS2.Application.Classification;
using BS2.Domain;
using BS2.Domain.Entities;
using Xunit;

namespace BS2.Tests.Classification;

public class TransactionHistoryTests
{
    private static readonly IReadOnlyList<Category> Categories = CategorySeed.Categories;
    private static readonly Category Groceries = CategorySeed.Resolve("Groceries")!;
    private static readonly Category Restaurant = Categories.First(c => c.Code != "Groceries");

    [Fact]
    public void Match_needs_two_agreeing_earlier_transactions_by_name()
    {
        var once = History(Tx("DELHAIZE 123 GENT", Groceries));
        var twice = History(Tx("DELHAIZE 123 GENT", Groceries), Tx("Delhaize-456 Gent", Groceries));

        Assert.Null(once.Match(Tx("Delhaize 789 Gent")));
        var match = twice.Match(Tx("Delhaize 789 Gent"));
        Assert.Equal(Groceries, match?.Category);
        Assert.Equal(1.0, match?.Share);
    }

    [Fact]
    public void Match_rejects_counterparties_used_for_several_categories()
    {
        var history = History(Tx("PAYPAL", Groceries), Tx("PAYPAL", Groceries), Tx("PAYPAL", Restaurant));

        Assert.Null(history.Match(Tx("PAYPAL")));
    }

    [Fact]
    public void Match_falls_back_to_name_when_shared_iban_disagrees()
    {
        var history = History(
            Tx("Shop A", Groceries, iban: "BE11 0000"), Tx("Shop A", Groceries, iban: "BE11 0000"),
            Tx("Shop B", Restaurant, iban: "BE11 0000"));

        Assert.Equal(Groceries, history.Match(Tx("Shop A", iban: "BE110000"))?.Category);
    }

    [Fact]
    public void Similar_ranks_rare_words_above_boilerplate_and_skips_unrelated()
    {
        var history = History(
            Tx("MERCADONA VALENCIA", Groceries, description: "Betaling Maestro"),
            Tx("BAR CENTRAL", Restaurant, description: "Betaling Maestro"),
            Tx("CAFE ROMA", Restaurant, description: "Betaling Maestro"),
            Tx("NETFLIX", Restaurant, description: "Domiciliering"));

        var similar = history.Similar(Tx("MERCADONA MADRID", description: "Betaling Maestro"), 15);

        Assert.Equal("MERCADONA VALENCIA", similar[0].Transaction.CounterpartyName);
        Assert.DoesNotContain(similar, e => e.Transaction.CounterpartyName == "NETFLIX");
    }

    [Fact]
    public void CandidateGroups_are_recent_groups_or_ones_with_the_same_counterparty()
    {
        var trip = Group("Italy", Tx("Autogrill", date: new DateOnly(2026, 8, 2)));
        var old = Group("Wedding", Tx("Florist", date: new DateOnly(2025, 1, 1)));
        var car = Group("Car", Tx("Garage Peeters", date: new DateOnly(2024, 3, 1)));
        var history = new TransactionHistory([], Categories, [trip, old, car]);

        var candidates = history.CandidateGroups(Tx("Garage Peeters", date: new DateOnly(2026, 8, 20)));

        Assert.Equal(["Car", "Italy"], candidates.Select(c => c.Name));
        Assert.Equal(["Autogrill"], candidates[1].Counterparties);
    }

    private static TransactionHistory History(params Transaction[] classified) => new(classified, Categories, []);

    private static Transaction Tx(string name, Category? category = null, string? iban = null, string description = "", DateOnly? date = null) => new()
    {
        Id = Guid.NewGuid(),
        CounterpartyName = name,
        CounterpartyIban = iban,
        Description = description,
        Amount = -10m,
        Date = date ?? new DateOnly(2026, 8, 20),
        CategoryId = category?.Id
    };

    private static Group Group(string name, params Transaction[] members)
    {
        var g = new Group { Id = Guid.NewGuid(), Name = name };
        foreach (var m in members)
            g.Transactions.Add(new TransactionGroup { Group = g, GroupId = g.Id, Transaction = m, TransactionId = m.Id });
        return g;
    }
}
