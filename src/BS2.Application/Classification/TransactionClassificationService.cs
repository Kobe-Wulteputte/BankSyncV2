using System.Text.Json;
using BS2.Application.Abstractions;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BS2.Application.Classification;

public sealed class TransactionClassificationService(IAppDbContext db, ITransactionClassifier classifier, IClock clock) : ITransactionClassificationService
{
    public const string HistoryModel = "history";
    private const int MaxErrorLength = 2000; // matches the column length
    private const int ExampleCount = 15;

    private IReadOnlyList<Category>? _categories;
    private readonly Dictionary<Guid, TransactionHistory> _history = [];

    public async Task<ClassificationRun> ClassifyAsync(Transaction transaction, ClassificationTrigger trigger, bool force, CancellationToken ct)
    {
        _categories ??= await db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync(ct);
        var history = await HistoryAsync(transaction.UserId, ct);

        // Skip the model only when it has nothing to add: with candidate groups it still has to pick one.
        var groups = history.CandidateGroups(transaction);
        var match = groups.Count == 0 ? history.Match(transaction) : null;
        var result = match is not null
            ? FromHistory(match)
            : await classifier.ClassifyAsync(transaction, _categories,
                new ClassificationContext(history.Similar(transaction, ExampleCount), groups), ct);
        var now = clock.UtcNow;

        var run = new ClassificationRun
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            Trigger = trigger,
            Model = result.Model,
            SystemPrompt = result.SystemPrompt,
            UserPrompt = result.UserPrompt,
            RawResponse = result.RawResponse,
            PredictedCategoryCode = result.PredictedCategoryCode,
            PredictedCategoryId = result.PredictedCategory?.Id,
            Confidence = result.Confidence,
            Threshold = result.Threshold,
            Accepted = result.Accepted,
            AlternativesJson = result.Alternatives.Count == 0
                ? null
                : JsonSerializer.Serialize(result.Alternatives.Select(a => new { code = a.Code, logprob = a.LogProb, probability = a.Probability })),
            SuggestedGroupId = result.SuggestedGroupId,
            PromptTokens = result.PromptTokens,
            CompletionTokens = result.CompletionTokens,
            LatencyMs = result.LatencyMs,
            Error = result.Error is { Length: > MaxErrorLength } e ? e[..MaxErrorLength] : result.Error,
            CreatedAt = now
        };
        db.ClassificationRuns.Add(run);

        var mayOverwrite = force || transaction.ClassificationSource != ClassificationSource.Manual;
        if (result.Accepted && result.PredictedCategory is not null && mayOverwrite)
        {
            transaction.CategoryId = result.PredictedCategory.Id;
            transaction.ClassificationSource = ClassificationSource.Ai;
            transaction.ClassifiedAt = now;
            transaction.UpdatedAt = now;
        }

        return run;
    }

    private static ClassificationResult FromHistory(HistoryMatch match) => new(
        HistoryModel, "", $"{match.Agreeing} of {match.Total} earlier transactions with this counterparty are {match.Category.Code}.",
        match.Category.Code, match.Category.Code, match.Category, match.Share, TransactionHistory.MinAgreement, true, [],
        null, null, 0, null);

    /// <summary>Loaded once per user per scope, so a sync batch reads the history once.</summary>
    private async Task<TransactionHistory> HistoryAsync(Guid userId, CancellationToken ct)
    {
        if (_history.TryGetValue(userId, out var cached)) return cached;

        var classified = await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.CategoryId != null)
            .ToListAsync(ct);
        var groups = await db.Groups.AsNoTracking()
            .Where(g => g.UserId == userId)
            .Include(g => g.Transactions).ThenInclude(m => m.Transaction)
            .ToListAsync(ct);
        return _history[userId] = new TransactionHistory(classified, _categories!, groups);
    }
}
