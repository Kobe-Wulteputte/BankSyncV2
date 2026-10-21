using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BS2.Application.Abstractions;
using BS2.Infrastructure.EnableBanking.Models;
using BS2.Infrastructure.EnableBanking.Models.Accounts;

namespace BS2.Infrastructure.EnableBanking;

/// <summary>
/// V1's <c>ExpenseService.CreateExpense(Transaction, bankName)</c>, minus the date defaulting that
/// <c>TransactionRules.EffectiveDate</c> now owns. Description format is kept byte-for-byte: the
/// classifier was fine-tuned on it.
/// </summary>
public static class TransactionMapper
{
    public static ProviderTransaction Map(Transaction t)
    {
        decimal.TryParse(t.TransactionAmount?.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);
        var direction = t.CreditDebitIndicator == "DBIT" ? -1 : 1;
        amount *= direction;

        var description = string.Join(" ", t.RemittanceInformation ?? []).Trim();
        var name = direction == -1 ? t.Creditor?.Name : t.Debtor?.Name;
        if (string.IsNullOrEmpty(name)) name = description;
        if (name == description) description = "";

        description = description + "(" + t.BankTransactionCode?.Code + " "
                      + t.DebtorAccountAdditionalIdentification?.FirstOrDefault()?.Identification
                      + t.CreditorAccountAdditionalIdentification?.FirstOrDefault()?.Identification + ")";

        var bookingDate = ParseDate(t.BookingDate);
        var valueDate = ParseDate(t.ValueDate);

        var externalId = t.EntryReference ?? t.TransactionId
            ?? FallbackId(amount, bookingDate, valueDate, description);

        return new ProviderTransaction(
            externalId,
            amount,
            t.TransactionAmount?.Currency ?? "EUR",
            bookingDate,
            valueDate,
            name ?? "",
            t.DebtorAccount?.Iban ?? t.CreditorAccount?.Iban,
            description,
            JsonSerializer.Serialize(t, EnableBankingJson.Options));
    }

    /// <summary>Null when unparseable; V1 defaulted to today, which made ids drift between runs.</summary>
    public static DateOnly? ParseDate(string? value)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var date)) return date;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime)) return DateOnly.FromDateTime(dateTime);
        return null;
    }

    private static string FallbackId(decimal amount, DateOnly? bookingDate, DateOnly? valueDate, string description)
    {
        var material = string.Join("|",
            amount.ToString(CultureInfo.InvariantCulture),
            bookingDate?.ToString("yyyy-MM-dd") ?? "",
            valueDate?.ToString("yyyy-MM-dd") ?? "",
            description);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
