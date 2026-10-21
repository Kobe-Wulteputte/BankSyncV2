using BS2.Application.Abstractions;
using BS2.Domain;
using BS2.Domain.Entities;

namespace BS2.Application.Classification;

/// <summary>One emitted token with its logprob and the model's top alternatives for that position. SDK-neutral.</summary>
public sealed record TokenLogProb(string Token, double LogProb, IReadOnlyList<(string Token, double LogProb)> TopAlternatives);

public sealed record ParsedClassification(
    string? PredictedCode,
    Category? Category,
    double? Confidence,
    IReadOnlyList<ClassificationAlternative> Alternatives);

/// <summary>Turns a raw completion plus logprobs into a category, a confidence and alternatives. Pure.</summary>
public static class ClassificationResponseParser
{
    public static ParsedClassification Parse(string? rawResponse, IReadOnlyList<Category> categories, IReadOnlyList<TokenLogProb> tokens)
    {
        var cleaned = Clean(rawResponse);
        var category = CategorySeed.Resolve(cleaned, categories);

        // V1: sequence probability = exp(sum of token logprobs).
        double? confidence = tokens.Count == 0 ? null : Probability(tokens.Sum(t => t.LogProb));

        // Alternatives come from the first token only: that is where the model picks the category.
        var alternatives = tokens.Count == 0
            ? []
            : tokens[0].TopAlternatives
                .Select(a => new ClassificationAlternative(
                    CategorySeed.Resolve(Clean(a.Token), categories)?.Code ?? a.Token,
                    a.LogProb,
                    Probability(a.LogProb)))
                .ToList();

        return new ParsedClassification(category?.Code ?? cleaned, category, confidence, alternatives);
    }

    /// <summary>V1 rule: apply when the sequence probability reaches the threshold and the text maps to a known category.</summary>
    public static bool Decide(double? confidence, double threshold, Category? resolvedCategory) =>
        resolvedCategory is not null && confidence is { } c && c >= threshold;

    private static double Probability(double logProb) => Math.Clamp(Math.Exp(logProb), 0, 1);

    private static string? Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = raw.Replace(" ", "").Replace("_", "").Replace("[", "").Replace("]", "").Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }
}
