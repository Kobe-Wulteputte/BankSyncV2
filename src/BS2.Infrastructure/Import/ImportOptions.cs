namespace BS2.Infrastructure.Import;

public sealed class ImportOptions
{
    public const string Section = "Import";

    /// <summary>
    /// Maps a V1 <c>Type</c> column value to a bank name. V1 wrote whatever the source gave it: bank
    /// names for recent rows, Nordigen ids (ARGENTA_ARSPBE22) for 2023+, and the statement type
    /// (Betaling Bancontact, CARD_PAYMENT) for CSV exports before that. Case-insensitive.
    /// Values not listed are used as-is, except <c>NAME_BIC</c> ids which map to <c>Name</c>.
    /// </summary>
    public Dictionary<string, string> BankAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Create categories the workbook uses that do not exist yet, instead of importing the row uncategorized.</summary>
    public bool CreateMissingCategories { get; set; } = true;
}
