using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BS2.Domain.Entities;

namespace BS2.Application.Classification;

/// <summary>Prompt text for the classifier. Pure.</summary>
public static class ClassificationPrompt
{
    private const int MaxDescriptionLength = 200;

    /// <summary>Only depends on the categories, so it stays identical between calls and benefits from prompt caching.</summary>
    public static string System(IEnumerable<Category> categories) => $"""
        You categorize the bank transactions of a Belgian household. Descriptions are often in Dutch or French.
        Pick exactly one category code from this list (code: meaning, kind):
        {string.Join("\n", categories.Select(c => $"- {c.Code}: {c.Name} ({c.Kind})"))}

        Earlier transactions of the household are the strongest evidence: the same counterparty usually gets the same category.
        For a counterparty you have not seen before, use what you know about the business; a foreign supermarket chain is still groceries.
        Amounts are negative for money going out and positive for money coming in.
        Give your confidence as a probability between 0 and 1, and up to 2 alternative categories with their probability.
        Be honest: a guess based on an unclear name deserves a low confidence.
        """;

    public static string User(Transaction tx, string bankName, ClassificationContext context)
    {
        var sb = new StringBuilder();
        if (context.Examples.Count > 0)
        {
            sb.AppendLine("Earlier transactions of this household, with their category:");
            foreach (var e in context.Examples)
                sb.AppendLine($"- {Line(e.Transaction)} => {e.Category.Code}");
            sb.AppendLine();
        }

        if (context.Groups.Count > 0)
        {
            sb.AppendLine("The household tags transactions with groups (trips, events, projects). Set group to the number of the group this transaction belongs to, or 0 if none. Most transactions belong to no group; only pick one when the transaction clearly fits it.");
            for (var i = 0; i < context.Groups.Count; i++)
            {
                var g = context.Groups[i];
                var examples = g.Counterparties.Count == 0 ? "" : $", e.g. {string.Join(", ", g.Counterparties)}";
                sb.AppendLine($"{i + 1}. {Clean(g.Name)} ({Date(g.From)} to {Date(g.To)}{examples})");
            }
            sb.AppendLine();
        }

        sb.Append($"Transaction to categorize: Bank: {Clean(bankName)}, {Line(tx)}");
        return sb.ToString();
    }

    private static string Line(Transaction t)
    {
        var description = Clean(t.Description);
        if (description.Length > MaxDescriptionLength) description = description[..MaxDescriptionLength] + "…";
        return $"Date: {Date(t.Date)}, Amount: {t.Amount.ToString("0.00", CultureInfo.InvariantCulture)} {t.Currency}, Name: {Clean(t.CounterpartyName)}, Description: {description}";
    }

    private static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Clean(string? input) =>
        string.IsNullOrWhiteSpace(input) ? "" : Regex.Replace(input.Trim(), @"\s+", " ");
}
