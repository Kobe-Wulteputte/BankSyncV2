using System.Text.Json.Serialization;

namespace BS2.Infrastructure.EnableBanking.Models.General;

public class GetASPSPsRequest
{
    public string? Country { get; set; }
    public string? PsuType { get; set; }
    public string? Service { get; set; }
    public string? PaymentType { get; set; }
}

public class GetASPSPsResponse
{
    [JsonPropertyName("aspsps")] public Aspsp[]? Aspsps { get; set; }
}

public class Aspsp
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
    [JsonPropertyName("logo")] public string? Logo { get; set; }
    [JsonPropertyName("psu_types")] public string[]? PsuTypes { get; set; }
    [JsonPropertyName("auth_methods")] public AuthMethod[]? AuthMethods { get; set; }
    [JsonPropertyName("maximum_consent_validity")] public int? MaximumConsentValidity { get; set; }
    [JsonPropertyName("sandbox")] public Sandbox? Sandbox { get; set; }
    [JsonPropertyName("beta")] public bool? Beta { get; set; }
    [JsonPropertyName("bic")] public string? Bic { get; set; }
    [JsonPropertyName("required_psu_headers")] public string[]? RequiredPsuHeaders { get; set; }
}

public class AuthMethod
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("psu_type")] public string? PsuType { get; set; }
    [JsonPropertyName("credentials")] public Credential[]? Credentials { get; set; }
    [JsonPropertyName("approach")] public string? Approach { get; set; }
    [JsonPropertyName("hidden_method")] public bool? HiddenMethod { get; set; }
}

public class Credential
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("required")] public bool? Required { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("template")] public string? Template { get; set; }
}

public class Sandbox
{
    [JsonPropertyName("users")] public User[]? Users { get; set; }
}

public class User
{
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("password")] public string? Password { get; set; }
    [JsonPropertyName("otp")] public string? Otp { get; set; }
}

public class GetApplicationResponse
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("kid")] public Guid? Kid { get; set; }
    [JsonPropertyName("environment")] public string? Environment { get; set; }
    [JsonPropertyName("redirect_urls")] public Uri[]? RedirectUrls { get; set; }
    [JsonPropertyName("active")] public bool? Active { get; set; }
    [JsonPropertyName("countries")] public string[]? Countries { get; set; }
    [JsonPropertyName("services")] public string[]? Services { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public class StartAuthorizationRequest
{
    [JsonPropertyName("access")] public Access? Access { get; set; }
    [JsonPropertyName("aspsp")] public Aspsp? Aspsp { get; set; }
    [JsonPropertyName("state")] public string? State { get; set; }
    [JsonPropertyName("redirect_url")] public Uri? RedirectUrl { get; set; }
    [JsonPropertyName("psu_type")] public string? PsuType { get; set; }
    [JsonPropertyName("auth_method")] public string? AuthMethod { get; set; }
    [JsonPropertyName("credentials")] public object? Credentials { get; set; }
    [JsonPropertyName("credentials_autosubmit")] public bool? CredentialsAutosubmit { get; set; }
    [JsonPropertyName("language")] public string? Language { get; set; }
    [JsonPropertyName("psu_id")] public string? PsuId { get; set; }
}

public class Access
{
    [JsonPropertyName("valid_until")] public DateTime? ValidUntil { get; set; }
    [JsonPropertyName("accounts")] public Account[]? Accounts { get; set; }
    [JsonPropertyName("balances")] public bool? Balances { get; set; }
    [JsonPropertyName("transactions")] public bool? Transactions { get; set; }
}

public class Account
{
    [JsonPropertyName("iban")] public string? Iban { get; set; }
    [JsonPropertyName("other")] public Other? Other { get; set; }
}

public class Other
{
    [JsonPropertyName("identification")] public string? Identification { get; set; }
    [JsonPropertyName("scheme_name")] public string? SchemeName { get; set; }
    [JsonPropertyName("issuer")] public string? Issuer { get; set; }
}

public class StartAuthorizationResponse
{
    [JsonPropertyName("url")] public Uri? Url { get; set; }
    [JsonPropertyName("authorization_id")] public Guid? AuthorizationId { get; set; }
    [JsonPropertyName("psu_id_hash")] public string? PsuIdHash { get; set; }
}
