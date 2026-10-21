using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Application.Transactions;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using BS2.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BS2.Tests.Transactions;

public class TransactionCommandServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private sealed class NoClassifier : ITransactionClassificationService
    {
        public Task<ClassificationRun> ClassifyAsync(Transaction transaction, ClassificationTrigger trigger, bool force, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private static TransactionCommandService Service(AppDbContext db)
    {
        var user = new StaticCurrentUser(TestDb.UserId);
        return new TransactionCommandService(db, user, new FixedClock(Now), new TransactionQueryService(db, user), new NoClassifier());
    }

    private static async Task<(AppDbContext Db, Transaction Tx, Group G1, Group G2)> Seed()
    {
        var db = TestDb.Create();
        var account = TestDb.SeedAccount(db);
        var g1 = TestDb.Group(name: "G1");
        var g2 = TestDb.Group(name: "G2");
        var tx = TestDb.Tx(account, -12.5m, new DateOnly(2026, 2, 1), categoryId: 1);
        tx.ClassificationSource = ClassificationSource.Ai;
        tx.ClassifiedAt = Now.AddDays(-1);
        tx.Groups.Add(new TransactionGroup { GroupId = g1.Id });
        db.Categories.AddRange(TestDb.Category(1), TestDb.Category(2), TestDb.Category(3, active: false));
        db.Groups.AddRange(g1, g2, TestDb.Group(userId: TestDb.OtherUserId, name: "Foreign"));
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, tx, g1, g2);
    }

    [Fact]
    public async Task Patch_notes_tri_state_and_length_limit()
    {
        var (db, tx, _, _) = await Seed();
        await using var _ = db;

        await Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(NotesSpecified: true, Notes: "hello"));
        var untouched = await Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(Reimbursed: true));
        Assert.Equal("hello", untouched.Notes);

        var cleared = await Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(NotesSpecified: true, Notes: null));
        Assert.Null(cleared.Notes);

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(NotesSpecified: true, Notes: new string('x', 2001))));
    }

    [Fact]
    public async Task Patch_without_category_leaves_classification_untouched()
    {
        var (db, tx, _, _) = await Seed();
        await using var _ = db;

        var dto = await Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(Reimbursed: true, NotesSpecified: true, Notes: "  paid back "));

        Assert.True(dto.Reimbursed);
        Assert.Equal("paid back", dto.Notes);
        Assert.Equal(1, dto.CategoryId);
        Assert.Equal(ClassificationSource.Ai, dto.ClassificationSource);
    }

    [Fact]
    public async Task Patch_with_category_marks_manual()
    {
        var (db, tx, _, _) = await Seed();
        await using var _ = db;

        var dto = await Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(CategorySpecified: true, CategoryId: 2));

        Assert.Equal(2, dto.CategoryId);
        Assert.Equal(ClassificationSource.Manual, dto.ClassificationSource);
        Assert.Equal(Now, dto.ClassifiedAt);
    }

    [Fact]
    public async Task Patch_with_null_category_clears_it()
    {
        var (db, tx, _, _) = await Seed();
        await using var _ = db;

        var dto = await Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(CategorySpecified: true, CategoryId: null));

        Assert.Null(dto.CategoryId);
        Assert.Equal(ClassificationSource.None, dto.ClassificationSource);
        Assert.Null(dto.ClassifiedAt);
    }

    [Fact]
    public async Task Patch_rejects_unknown_or_inactive_category()
    {
        var (db, tx, _, _) = await Seed();
        await using var _ = db;

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(CategorySpecified: true, CategoryId: 3)));
        Assert.Contains("categoryId", ex.Errors.Keys);
        await Assert.ThrowsAsync<ValidationException>(() => Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(CategorySpecified: true, CategoryId: 999)));
    }

    [Fact]
    public async Task Patch_replaces_groups()
    {
        var (db, tx, g1, g2) = await Seed();
        await using var _ = db;

        var dto = await Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(GroupIds: [g2.Id]));

        Assert.Equal([g2.Id], dto.GroupIds);
        Assert.DoesNotContain(g1.Id, dto.GroupIds);
        Assert.Equal(1, await db.TransactionGroups.CountAsync(tg => tg.TransactionId == tx.Id));
    }

    [Fact]
    public async Task Patch_rejects_foreign_group()
    {
        var (db, tx, _, _) = await Seed();
        await using var _ = db;
        var foreign = await db.Groups.SingleAsync(g => g.UserId == TestDb.OtherUserId);

        await Assert.ThrowsAsync<ValidationException>(() => Service(db).PatchAsync(tx.Id, new PatchTransactionRequest(GroupIds: [foreign.Id])));
    }

    [Fact]
    public async Task Patch_unknown_or_foreign_transaction_is_not_found()
    {
        var (db, _, _, _) = await Seed();
        await using var _ = db;
        var foreignTx = TestDb.Tx(TestDb.SeedAccount(db, TestDb.OtherUserId), -1, new DateOnly(2026, 1, 1));
        db.Transactions.Add(foreignTx);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).PatchAsync(Guid.NewGuid(), new PatchTransactionRequest()));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).PatchAsync(foreignTx.Id, new PatchTransactionRequest()));
    }

    [Fact]
    public async Task Bulk_updates_only_own_rows_and_adjusts_groups()
    {
        var (db, tx, g1, g2) = await Seed();
        await using var _ = db;
        var account = await db.Accounts.SingleAsync(a => a.UserId == TestDb.UserId);
        var second = TestDb.Tx(account, -5, new DateOnly(2026, 2, 2));
        var foreignTx = TestDb.Tx(TestDb.SeedAccount(db, TestDb.OtherUserId), -1, new DateOnly(2026, 1, 1));
        db.Transactions.AddRange(second, foreignTx);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var updated = await Service(db).BulkAsync(new BulkRequest(
            [tx.Id, second.Id, foreignTx.Id, Guid.NewGuid()],
            CategorySpecified: true, CategoryId: 2, Reimbursed: true, AddGroupIds: [g2.Id], RemoveGroupIds: [g1.Id]));

        Assert.Equal(2, updated);
        db.ChangeTracker.Clear();
        var rows = await db.Transactions.Include(t => t.Groups).Where(t => t.UserId == TestDb.UserId).ToListAsync();
        Assert.All(rows, r =>
        {
            Assert.Equal(2, r.CategoryId);
            Assert.Equal(ClassificationSource.Manual, r.ClassificationSource);
            Assert.True(r.Reimbursed);
            Assert.Equal([g2.Id], r.Groups.Select(g => g.GroupId));
        });
        var untouched = await db.Transactions.SingleAsync(t => t.Id == foreignTx.Id);
        Assert.Null(untouched.CategoryId);
    }

    [Fact]
    public async Task Bulk_with_empty_ids_is_noop()
    {
        var (db, _, _, _) = await Seed();
        await using var _ = db;
        Assert.Equal(0, await Service(db).BulkAsync(new BulkRequest([], CategorySpecified: true, CategoryId: 2)));
    }
}
