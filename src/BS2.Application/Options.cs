namespace BS2.Application;

public sealed class SyncOptions
{
    public const string Section = "Sync";

    /// <summary>How far back each sync fetches. ASPSPs rate-limit transaction calls, so keep it short.</summary>
    public int RetrievalDays { get; set; } = 31;

    /// <summary>Background sync interval. Zero disables the scheduler.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Delay before the first scheduled sync after startup.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Consent length requested when authorizing, unless the connection overrides it.</summary>
    public int ConsentValidityDays { get; set; } = 90;

    /// <summary>A session this close to expiry is treated as needing re-authorization.</summary>
    public int RenewBeforeDays { get; set; } = 1;

    /// <summary>How long a started authorization link stays usable before a new one is generated.</summary>
    public int AuthorizationLinkTtlHours { get; set; } = 24;

    /// <summary>Email address that receives expiry notifications. Empty disables mail.</summary>
    public string NotifyEmail { get; set; } = string.Empty;
}

public sealed class ClassificationOptions
{
    public const string Section = "Classification";

    public string Model { get; set; } = string.Empty;

    /// <summary>Minimum sequence probability (0..1) for a prediction to be applied. V1 used 60%.</summary>
    public double Threshold { get; set; } = 0.6;

    public int TopLogProbs { get; set; } = 5;

    /// <summary>Classify new transactions during sync. Off saves model calls while testing.</summary>
    public bool ClassifyOnSync { get; set; } = true;
}

public sealed class Auth0Options
{
    public const string Section = "Auth0";

    public string Domain { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Auth0 <c>sub</c> values allowed in. Empty list means nobody.</summary>
    public List<string> AllowedSubjects { get; set; } = [];

    /// <summary>Alternative to subjects: emails allowed in (requires an email claim in the token).</summary>
    public List<string> AllowedEmails { get; set; } = [];

    /// <summary>May change shared categories and classes and run the Excel import. Must also be allow-listed.</summary>
    public List<string> AdminSubjects { get; set; } = [];

    public List<string> AdminEmails { get; set; } = [];
}

public sealed class EncryptionOptions
{
    public const string Section = "Encryption";

    /// <summary>Base64-encoded 32-byte key. Generate with <c>openssl rand -base64 32</c>.</summary>
    public string MasterKey { get; set; } = string.Empty;
}

public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>Public origin of this API, used to build the bank redirect URL. e.g. https://localhost:8080</summary>
    public string PublicOrigin { get; set; } = "https://localhost:8080";

    /// <summary>Where the SPA lives when served separately (dev). Empty = same origin.</summary>
    public string ClientOrigin { get; set; } = string.Empty;
}
