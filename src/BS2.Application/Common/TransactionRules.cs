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
}
