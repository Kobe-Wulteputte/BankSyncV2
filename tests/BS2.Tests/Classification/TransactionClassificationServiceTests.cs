using System.Text.Json;
using BS2.Application.Abstractions;
using BS2.Application.Classification;
using BS2.Domain;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using BS2.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BS2.Tests.Classification;

public class TransactionClassificationServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Stores_run_with_all_fields_and_applies_category_when_accepted()
    {
        await using var db = NewDb();
        var tx = await SeedAsync(db, ClassificationSource.None);
        var groceries = db.Categories.Single(c => c.Code == "Groceries");
        var classifier = new FakeClassifier(Accepted(groceries));
        var service = new TransactionClassificationService(db, classifier, new FakeClock(Now));

        var run = await service.ClassifyAsync(tx, ClassificationTrigger.Sync, force: false, CancellationToken.None);
        await db.SaveChangesAsync();

        var stored = await db.ClassificationRuns.SingleAsync();
        Assert.Equal(run.Id, stored.Id);
        Assert.Equal(tx.Id, stored.TransactionId);
        Assert.Equal(ClassificationTrigger.Sync, stored.Trigger);
        Assert.Equal("ft:model", stored.Model);
        Assert.Equal("sys", stored.SystemPrompt);
        Assert.Equal("user", stored.UserPrompt);
        Assert.Equal("Groceries", stored.RawResponse);
        Assert.Equal("Groceries", stored.PredictedCategoryCode);
        Assert.Equal(groceries.Id, stored.PredictedCategoryId);
        Assert.Equal(0.9, stored.Confidence);
        Assert.Equal(0.6, stored.Threshold);
        Assert.True(stored.Accepted);
        Assert.Equal(12, stored.PromptTokens);
        Assert.Equal(3, stored.CompletionTokens);
        Assert.Equal(250, stored.LatencyMs);
        Assert.Null(stored.Error);
        Assert.Equal(Now, stored.CreatedAt);

        using var json = JsonDocument.Parse(stored.AlternativesJson!);
        var first = json.RootElement[0];
        Assert.Equal("Groceries", first.GetProperty("code").GetString());
        Assert.Equal(Math.Log(0.9), first.GetProperty("logprob").GetDouble(), 6);
        Assert.Equal(0.9, first.GetProperty("probability").GetDouble(), 6);

        Assert.Equal(groceries.Id, tx.CategoryId);
        Assert.Equal(ClassificationSource.Ai, tx.ClassificationSource);
        Assert.Equal(Now, tx.ClassifiedAt);
        Assert.Equal(Now, tx.UpdatedAt);
    }

    [Fact]
    public async Task Passes_seed_order_regardless_of_db_order_and_appends_active_extras()
    {
        await using var db = NewDb();
        var tx = await SeedAsync(db, ClassificationSource.None);
        db.Categories.Single(c => c.Code == "Clothes").SortOrder = 999; // user reordered in the UI
        db.Categories.Add(new Category { Id = 100, Code = "Pets", Name = "Pets", Kind = CategoryKind.Expense, SortOrder = 50, IsActive = true });
        db.Categories.Add(new Category { Id = 101, Code = "Old", Name = "Old", Kind = CategoryKind.Expense, SortOrder = 51, IsActive = false });
        await db.SaveChangesAsync();
        var classifier = new FakeClassifier(Accepted(db.Categories.Single(c => c.Code == "Groceries")));
        var service = new TransactionClassificationService(db, classifier, new FakeClock(Now));

        await service.ClassifyAsync(tx, ClassificationTrigger.Sync, force: false, CancellationToken.None);

        var expected = CategorySeed.Categories.Select(c => c.Code).Append("Pets");
        Assert.Equal(expected, classifier.LastCategories!.Select(c => c.Code));
    }

    [Fact]
    public async Task Does_not_overwrite_manual_classification_unless_forced()
    {
        await using var db = NewDb();
        var tx = await SeedAsync(db, ClassificationSource.Manual, categoryCode: "Gifts");
        var groceries = db.Categories.Single(c => c.Code == "Groceries");
        var service = new TransactionClassificationService(db, new FakeClassifier(Accepted(groceries)), new FakeClock(Now));

        var run = await service.ClassifyAsync(tx, ClassificationTrigger.Backfill, force: false, CancellationToken.None);

        Assert.True(run.Accepted);
        Assert.Equal("Gifts", db.Categories.Single(c => c.Id == tx.CategoryId).Code);
        Assert.Equal(ClassificationSource.Manual, tx.ClassificationSource);

        await service.ClassifyAsync(tx, ClassificationTrigger.Manual, force: true, CancellationToken.None);

        Assert.Equal(groceries.Id, tx.CategoryId);
        Assert.Equal(ClassificationSource.Ai, tx.ClassificationSource);
        Assert.Equal(2, db.ChangeTracker.Entries<ClassificationRun>().Count());
    }

    [Fact]
    public async Task Stores_error_run_without_touching_transaction()
    {
        await using var db = NewDb();
        var tx = await SeedAsync(db, ClassificationSource.None);
        var error = new ClassificationResult("ft:model", "sys", "user", null, null, null, null, 0.6, false, [], null, null, 1200, "rate limited");
        var service = new TransactionClassificationService(db, new FakeClassifier(error), new FakeClock(Now));

        await service.ClassifyAsync(tx, ClassificationTrigger.Sync, force: false, CancellationToken.None);
        await db.SaveChangesAsync();

        var stored = await db.ClassificationRuns.SingleAsync();
        Assert.False(stored.Accepted);
        Assert.Equal("rate limited", stored.Error);
        Assert.Null(stored.AlternativesJson);
        Assert.Null(stored.PredictedCategoryId);
        Assert.Null(tx.CategoryId);
        Assert.Equal(ClassificationSource.None, tx.ClassificationSource);
        Assert.Null(tx.ClassifiedAt);
    }

    private static ClassificationResult Accepted(Category category) => new(
        "ft:model", "sys", "user", category.Code, category.Code, category, 0.9, 0.6, true,
        [new ClassificationAlternative(category.Code, Math.Log(0.9), 0.9), new ClassificationAlternative("Food", Math.Log(0.05), 0.05)],
        12, 3, 250, null);

    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options, new PassThroughEncryptor());
    }

    private static async Task<Transaction> SeedAsync(AppDbContext db, ClassificationSource source, string? categoryCode = null)
    {
        foreach (var c in CategorySeed.Categories)
            db.Categories.Add(new Category { Id = c.Id, Code = c.Code, Name = c.Name, Kind = c.Kind, SortOrder = c.SortOrder, IsActive = c.Code != "Chiro" });
        await db.SaveChangesAsync();

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            ExternalId = "ext-1",
            Amount = -12.5m,
            Date = new DateOnly(2026, 10, 1),
            CounterpartyName = "Colruyt",
            Description = "Groceries",
            ClassificationSource = source,
            CategoryId = categoryCode is null ? null : db.Categories.Single(c => c.Code == categoryCode).Id
        };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();
        return tx;
    }
}

file sealed class FakeClassifier(ClassificationResult result) : ITransactionClassifier
{
    public IReadOnlyList<Category>? LastCategories { get; private set; }

    public Task<ClassificationResult> ClassifyAsync(Transaction transaction, IReadOnlyList<Category> categories, CancellationToken ct)
    {
        LastCategories = categories;
        return Task.FromResult(result);
    }
}

file sealed class FakeClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow => utcNow;
}

file sealed class PassThroughEncryptor : IFieldEncryptor
{
    public string Encrypt(string plaintext) => plaintext;
    public string Decrypt(string ciphertext) => ciphertext;
    public string Hash(string value) => value;
}
