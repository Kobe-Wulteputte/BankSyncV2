using System.Globalization;
using BS2.Domain.Entities;
using BS2.Domain.Enums;

namespace BS2.Application.Common;

/// <summary>Pure rules shared by sync, import and analytics. No I/O.</summary>
public static class TransactionRules
{
    /// <summary>V1 rule: the earlier of value and booking date; today when neither is known.</summary>
    public static DateOnly EffectiveDate(DateOnly? valueDate, DateOnly? bookingDate, DateOnly today)
    {
        if (valueDate is null && bookingDate is null) return today;
        if (valueDate is null) return bookingDate!.Value;
        if (bookingDate is null) return valueDate.Value;
        return valueDate.Value < bookingDate.Value ? valueDate.Value : bookingDate.Value;
    }

    public static bool IsExpense(decimal amount, CategoryKind? kind) =>
        amount < 0 && kind != CategoryKind.Transfer;

    public static bool IsIncome(decimal amount, CategoryKind? kind) =>
        amount > 0 && kind != CategoryKind.Transfer;

    /// <summary>
    /// V1 formatted the amount with the current culture, and the training data was generated on an
    /// en-BE machine ("-12,5", no grouping). Pinned so a Linux container produces the same prompt.
    /// </summary>
    public static readonly CultureInfo PromptCulture = CultureInfo.GetCultureInfo("en-BE");

    /// <summary>Prompt text for the classifier. Must stay byte-identical to V1's <c>Expense.AiPrompt</c>: the model was fine-tuned on it.</summary>
    public static string BuildAiPrompt(string bankName, decimal amount, DateOnly date, string counterpartyName, string description) =>
        $"Type: [{bankName}], Amount: [{amount.ToString(PromptCulture)}], Date: [{date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}], Name: [{Clean(counterpartyName)}], Description: [{Clean(description)}]";

    public static string BuildAiPrompt(Transaction t, string bankName) =>
        BuildAiPrompt(bankName, t.Amount, t.Date, t.CounterpartyName, t.Description);

    /// <summary>V1 system prompt, verbatim. Lists category codes, not display names.</summary>
    public static string BuildSystemPrompt(IEnumerable<Category> categories) =>
        $"You categorize bank transactions. Reply with exactly one of these categories: {string.Join(", ", categories.Select(c => c.Code))}.";

    private static string Clean(string? input)
    {
        var cleaned = string.IsNullOrWhiteSpace(input) ? "" : input.Trim();
        cleaned = cleaned.Replace("\n", " ").Replace("\r", " ");
        return System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
    }
}
