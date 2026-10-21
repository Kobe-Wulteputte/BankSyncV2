using BS2.Infrastructure.EnableBanking;
using BS2.Infrastructure.EnableBanking.Models.Accounts;
using Xunit;

namespace BS2.Tests.EnableBanking;

public class TransactionMapperTests
{
    private static Transaction Debit() => new()
    {
        TransactionAmount = new AmountDetails { Amount = "12.50", Currency = "EUR" },
        CreditDebitIndicator = "DBIT",
        Creditor = new PartyDetails { Name = "Colruyt" },
        Debtor = new PartyDetails { Name = "Me" },
        CreditorAccount = new AccountDetails { Iban = "BE68 5390 0754 7034" },
        RemittanceInformation = ["Groceries", "week 3"],
        BankTransactionCode = new BankTransactionCode { Code = "PMNT" },
        BookingDate = "2026-01-15",
        ValueDate = "2026-01-14",
        EntryReference = "ref-1"
    };

    [Fact]
    public void Debit_is_negative_and_uses_creditor_name()
    {
        var result = TransactionMapper.Map(Debit());

        Assert.Equal(-12.50m, result.Amount);
        Assert.Equal("Colruyt", result.CounterpartyName);
        Assert.Equal("Groceries week 3(PMNT )", result.Description);
        Assert.Equal("BE68 5390 0754 7034", result.CounterpartyIban);
        Assert.Equal(new DateOnly(2026, 1, 15), result.BookingDate);
        Assert.Equal(new DateOnly(2026, 1, 14), result.ValueDate);
        Assert.Equal("ref-1", result.ExternalId);
        Assert.Equal("EUR", result.Currency);
        Assert.Contains("\"credit_debit_indicator\":\"DBIT\"", result.RawJson);
    }

    [Fact]
    public void Credit_is_positive_and_uses_debtor_name()
    {
        var t = Debit();
        t.CreditDebitIndicator = "CRDT";

        var result = TransactionMapper.Map(t);

        Assert.Equal(12.50m, result.Amount);
        Assert.Equal("Me", result.CounterpartyName);
    }

    [Fact]
    public void Missing_name_falls_back_to_remittance_and_empties_description()
    {
        var t = Debit();
        t.Creditor = null;

        var result = TransactionMapper.Map(t);

        Assert.Equal("Groceries week 3", result.CounterpartyName);
        Assert.Equal("(PMNT )", result.Description);
    }

    [Fact]
    public void Unparseable_dates_become_null()
    {
        var t = Debit();
        t.BookingDate = "not a date";
        t.ValueDate = null;

        var result = TransactionMapper.Map(t);

        Assert.Null(result.BookingDate);
        Assert.Null(result.ValueDate);
    }

    [Fact]
    public void Fallback_external_id_is_stable_across_mappings()
    {
        var t = Debit();
        t.EntryReference = null;
        t.TransactionId = null;

        var first = TransactionMapper.Map(t).ExternalId;
        var again = TransactionMapper.Map(t).ExternalId;

        t.TransactionAmount!.Amount = "12.51";
        var different = TransactionMapper.Map(t).ExternalId;

        Assert.Equal(64, first.Length);
        Assert.Equal(first, again);
        Assert.NotEqual(first, different);
    }

    [Fact]
    public void Transaction_id_is_used_when_entry_reference_missing()
    {
        var t = Debit();
        t.EntryReference = null;
        t.TransactionId = "tx-9";

        Assert.Equal("tx-9", TransactionMapper.Map(t).ExternalId);
    }
}
