using System.Text.Json;
using BS2.Application.Abstractions;
using BS2.Domain;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BS2.Application.Classification;

public sealed class TransactionClassificationService(IAppDbContext db, ITransactionClassifier classifier, IClock clock) : ITransactionClassificationService
{
    private const int MaxErrorLength = 2000; // matches the column length

    private IReadOnlyList<Category>? _categories;

    public async Task<ClassificationRun> ClassifyAsync(Transaction transaction, ClassificationTrigger trigger, bool force, CancellationToken ct)
    {
        _categories ??= await LoadCategoriesAsync(ct);

        var result = await classifier.ClassifyAsync(transaction, _categories, ct);
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

    /// <summary>
    /// The fine-tuned model was trained on V1's exact category order, so the seed order wins over
    /// DB order/active flags; categories added later are appended by SortOrder.
    /// </summary>
    private async Task<IReadOnlyList<Category>> LoadCategoriesAsync(CancellationToken ct)
    {
        var all = await db.Categories.ToListAsync(ct);
        var byCode = all.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var seedCodes = CategorySeed.Categories.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ordered = CategorySeed.Categories
            .Select(s => byCode.GetValueOrDefault(s.Code))
            .OfType<Category>()
            .ToList();
        ordered.AddRange(all.Where(c => c.IsActive && !seedCodes.Contains(c.Code)).OrderBy(c => c.SortOrder));
        return ordered;
    }
}
