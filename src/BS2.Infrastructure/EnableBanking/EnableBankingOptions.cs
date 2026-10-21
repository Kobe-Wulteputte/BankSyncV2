namespace BS2.Infrastructure.EnableBanking;

/// <summary>Bound from the "EnableBanking" section. Replaces V1's TokenHandlerOptions + EnableBankingSettings auth fields.</summary>
public sealed class EnableBankingOptions
{
    public const string Section = "EnableBanking";

    /// <summary>Path to the RSA private key PEM. Relative paths resolve against <see cref="AppContext.BaseDirectory"/>.</summary>
    public string KeyPath { get; set; } = string.Empty;

    /// <summary>Inline PEM content (for env-var based deployments). Wins over <see cref="KeyPath"/> when set.</summary>
    public string KeyPem { get; set; } = string.Empty;

    public string AppKid { get; set; } = string.Empty;

    /// <summary>
    /// Sent as the <c>psu-ip-address</c> header, which some ASPSPs require (Argenta). Leave unset to
    /// fall back to the machine's local IPv4.
    /// </summary>
    public string PsuIpAddress { get; set; } = string.Empty;

    public string ReadKeyPem()
    {
        if (!string.IsNullOrWhiteSpace(KeyPem)) return KeyPem;
        if (string.IsNullOrWhiteSpace(KeyPath))
            throw new InvalidOperationException("EnableBanking:KeyPem or EnableBanking:KeyPath must be configured.");

        // IIS/services start in system32, so a relative path must not depend on the working directory.
        var path = Path.IsPathRooted(KeyPath) ? KeyPath : Path.Combine(AppContext.BaseDirectory, KeyPath);
        return File.ReadAllText(path);
    }
}
