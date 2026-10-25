using System.Text.Json;
using BS2.Application.Abstractions;
using BS2.Domain;
using BS2.Domain.Entities;

namespace BS2.Application.Classification;

public sealed record ParsedClassification(
    string? PredictedCode,
    Category? Category,
    double? Confidence,
    IReadOnlyList<ClassificationAlternative> Alternatives,
    Guid? SuggestedGroupId);

/// <summary>
/// Turns the model's JSON answer (<c>{"category", "confidence", "alternatives": [{"category", "confidence"}], "group"}</c>)
/// into a category, a confidence, alternatives and a suggested group. Pure.
/// </summary>
public static class ClassificationResponseParser
{
    private static readonly ParsedClassification Nothing = new(null, null, null, [], null);

    public static ParsedClassification Parse(string? rawResponse, IReadOnlyList<Category> categories, IReadOnlyList<GroupCandidate> groups)
    {
        if (string.IsNullOrWhiteSpace(rawResponse)) return Nothing;
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Nothing;

            var code = String(root, "category");
            var category = CategorySeed.Resolve(code, categories);

            var alternatives = root.TryGetProperty("alternatives", out var alts) && alts.ValueKind == JsonValueKind.Array
                ? alts.EnumerateArray()
                    .Where(a => a.ValueKind == JsonValueKind.Object && String(a, "category") is not null)
                    .Select(a => Alternative(CategorySeed.Resolve(String(a, "category"), categories)?.Code ?? String(a, "category")!, Number(a, "confidence") ?? 0))
                    .ToList()
                : [];

            Guid? group = int.TryParse(String(root, "group"), out var n) && n >= 1 && n <= groups.Count ? groups[n - 1].Id : null;

            return new ParsedClassification(category?.Code ?? code, category, Number(root, "confidence"), alternatives, group);
        }
        catch (JsonException)
        {
            return Nothing;
        }
    }

    /// <summary>Apply when the confidence reaches the threshold and the answer maps to a known category.</summary>
    public static bool Decide(double? confidence, double threshold, Category? resolvedCategory) =>
        resolvedCategory is not null && confidence is { } c && c >= threshold;

    // LogProb is derived so stored runs keep one shape; floored because JSON cannot hold -Infinity.
    private static ClassificationAlternative Alternative(string code, double probability) =>
        new(code, Math.Log(Math.Max(probability, 1e-6)), probability);

    private static string? String(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null
        } : null;

    private static double? Number(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? Math.Clamp(v.GetDouble(), 0, 1) : null;
}
