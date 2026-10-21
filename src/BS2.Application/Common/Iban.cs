namespace BS2.Application.Common;

public static class Iban
{
    /// <summary>Strips spaces and punctuation and uppercases, so formatting cannot cause a silent mismatch.</summary>
    public static string Normalize(string? iban) =>
        new string((iban ?? "").Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>Last four characters only, for logs: the identifier column is encrypted, the log is not.</summary>
    public static string Mask(string? identifier) =>
        identifier is { Length: > 4 } ? $"…{identifier[^4..]}" : "…";
}
