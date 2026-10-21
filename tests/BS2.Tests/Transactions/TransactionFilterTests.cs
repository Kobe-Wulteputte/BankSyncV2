using BS2.Application.Transactions;
using BS2.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BS2.Tests.Transactions;

public class TransactionFilterTests
{
    private static readonly DateOnly D1 = new(2026, 1, 10);
    private static readonly DateOnly D2 = new(2026, 1, 20);
    private static readonly DateOnly D3 = new(2026, 2, 5);

    private static async Task<List<decimal>> Run(TransactionFilter filter, Action<Account, List<Transaction>, List<Group>>? extra = null)
    {
        await using var db = TestDb.Create();
        var account = TestDb.SeedAccount(db);
        var txs = new List<Transaction>
        {
            TestDb.Tx(account, -10, D1, categoryId: 1),
            TestDb.Tx(account, -20, D2, categoryId: null),
            TestDb.Tx(account, -30, D3, categoryId: 2, reimbursed: true),
            TestDb.Tx(account, -40, D3, categoryId: 1, userId: TestDb.OtherUserId)
        };
        var groups = new List<Group>();
        extra?.Invoke(account, txs, groups);
        db.Categories.AddRange(TestDb.Category(1), TestDb.Category(2));
        db.Groups.AddRange(groups);
        db.Transactions.AddRange(txs);
        await db.SaveChangesAsync();

        return await TransactionFilter.Apply(db.Transactions, filter, TestDb.UserId).Select(t => t.Amount).OrderBy(a => a).ToListAsync();
    }

    [Fact]
    public async Task Always_scopes_to_user() =>
        Assert.Equal([-30m, -20m, -10m], await Run(new TransactionFilter()));

    [Fact]
    public async Task Date_bounds_are_inclusive() =>
        Assert.Equal([-20m, -10m], await Run(new TransactionFilter(From: D1, To: D2)));

    [Fact]
    public async Task Category_zero_means_uncategorized() =>
        Assert.Equal([-20m], await Run(new TransactionFilter(CategoryIds: [0])));

    [Fact]
    public async Task Category_zero_combines_with_real_ids() =>
        Assert.Equal([-20m, -10m], await Run(new TransactionFilter(CategoryIds: [0, 1])));

    [Fact]
    public async Task Exclude_category_zero_drops_uncategorized() =>
        Assert.Equal([-30m, -10m], await Run(new TransactionFilter(ExcludeCategoryIds: [0])));

    [Fact]
    public async Task Exclude_category_ids() =>
        Assert.Equal([-30m, -20m], await Run(new TransactionFilter(ExcludeCategoryIds: [1])));

    [Fact]
    public async Task Exclude_reimbursed() =>
        Assert.Equal([-20m, -10m], await Run(new TransactionFilter(ExcludeReimbursed: true)));

    [Fact]
    public async Task Account_filter()
    {
        await using var db = TestDb.Create();
        var a1 = TestDb.SeedAccount(db);
        var a2 = TestDb.SeedAccount(db, bank: "Other");
        db.Transactions.AddRange(TestDb.Tx(a1, -1, D1), TestDb.Tx(a2, -2, D1));
        await db.SaveChangesAsync();

        var result = await TransactionFilter.Apply(db.Transactions, new TransactionFilter(AccountIds: [a2.Id]), TestDb.UserId).ToListAsync();
        Assert.Equal(-2m, Assert.Single(result).Amount);
    }

    [Fact]
    public async Task Group_include_and_exclude()
    {
        var tripId = Guid.NewGuid();
        var workId = Guid.NewGuid();
        // Fresh entity instances per run: each Run() builds its own context.
        void Tag(Account _, List<Transaction> txs, List<Group> groups)
        {
            groups.Add(new Group { Id = tripId, UserId = TestDb.UserId, Name = "Trip" });
            groups.Add(new Group { Id = workId, UserId = TestDb.UserId, Name = "Work" });
            txs[0].Groups.Add(new TransactionGroup { GroupId = tripId });
            txs[1].Groups.Add(new TransactionGroup { GroupId = workId });
        }

        Assert.Equal([-10m], await Run(new TransactionFilter(GroupIds: [tripId]), Tag));
        Assert.Equal([-30m, -20m], await Run(new TransactionFilter(ExcludeGroupIds: [tripId]), Tag));
        Assert.Equal([-30m], await Run(new TransactionFilter(ExcludeGroupIds: [tripId, workId]), Tag));
    }

    [Fact]
    public void Search_is_case_insensitive_on_counterparty_and_description()
    {
        var rows = new[]
        {
            new Transaction { CounterpartyName = "Albert Heijn", Description = "groceries" },
            new Transaction { CounterpartyName = "Shell", Description = "Fuel ALBERT street" },
            new Transaction { CounterpartyName = "Netflix", Description = "" }
        };
        Assert.Equal(2, TransactionFilter.ApplySearch(rows, "albert").Count());
        Assert.Equal(3, TransactionFilter.ApplySearch(rows, "  ").Count());
    }
}
