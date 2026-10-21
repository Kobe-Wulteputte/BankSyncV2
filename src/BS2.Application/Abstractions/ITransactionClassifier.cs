using BS2.Domain.Entities;

namespace BS2.Application.Abstractions;

/// <summary>Calls the model once and reports everything it said. Never throws for model errors: see <see cref="ClassificationResult.Error"/>.</summary>
public interface ITransactionClassifier
{
    Task<ClassificationResult> ClassifyAsync(Transaction transaction, IReadOnlyList<Category> categories, CancellationToken ct);
}

public sealed record ClassificationAlternative(string Code, double LogProb, double Probability);

public sealed record ClassificationResult(
    string Model,
    string SystemPrompt,
    string UserPrompt,
    string? RawResponse,
    string? PredictedCategoryCode,
    Category? PredictedCategory,
    double? Confidence,
    double Threshold,
    bool Accepted,
    IReadOnlyList<ClassificationAlternative> Alternatives,
    int? PromptTokens,
    int? CompletionTokens,
    int LatencyMs,
    string? Error);
