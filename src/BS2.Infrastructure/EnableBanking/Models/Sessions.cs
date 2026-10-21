using System.Text.Json.Serialization;

namespace BS2.Infrastructure.EnableBanking.Models.Sessions;

public class AuthorizeSessionRequest
{
    [JsonPropertyName("code")] public string? Code { get; set; }
}

public class AuthorizeSessionResponse
{
    [JsonPropertyName("session_id")] public string? SessionId { get; set; }
    [JsonPropertyName("accounts")] public Account[]? Accounts { get; set; }
    [JsonPropertyName("aspsp")] public Aspsp? Aspsp { get; set; }
    [JsonPropertyName("psu_type")] public string? PsuType { get; set; }
    [JsonPropertyName("access")] public Access? Access { get; set; }
}

public class Account
{
    [JsonPropertyName("cash_account_type")] public string? CashAccountType { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("identification_hash")] public string? IdentificationHash { get; set; }
    [JsonPropertyName("identification_hashes")] public string[]? IdentificationHashes { get; set; }
    [JsonPropertyName("account_id")] public AccountId? AccountId { get; set; }
    [JsonPropertyName("all_account_ids")] public AccountId[]? AllAccountIds { get; set; }
    [JsonPropertyName("account_servicer")] public AccountServicer? AccountServicer { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("details")] public string? Details { get; set; }
    [JsonPropertyName("usage")] public string? Usage { get; set; }
    [JsonPropertyName("product")] public string? Product { get; set; }
    [JsonPropertyName("psu_status")] public string? PsuStatus { get; set; }
    [JsonPropertyName("credit_limit")] public CreditLimit? CreditLimit { get; set; }
    [JsonPropertyName("legal_age")] public bool? LegalAge { get; set; }
    [JsonPropertyName("uid")] public string? Uid { get; set; }
}

public class AccountId
{
    [JsonPropertyName("iban")] public string? Iban { get; set; }
    [JsonPropertyName("other")] public Other? Other { get; set; }
}

public class AccountServicer
{
    [JsonPropertyName("bic_fi")] public string? BicFi { get; set; }
    [JsonPropertyName("clearing_system_member_id")] public ClearingSystemMemberId? ClearingSystemMemberId { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public class ClearingSystemMemberId
{
    [JsonPropertyName("clearing_system_id")] public string? ClearingSystemId { get; set; }
    [JsonPropertyName("member_id")] public string? MemberId { get; set; }
}

public class CreditLimit
{
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("amount")] public string? Amount { get; set; }
}

public class DeleteSessionResponse
{
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class GetSessionResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("accounts")] public string[]? Accounts { get; set; }
    [JsonPropertyName("accounts_data")] public AccountData[]? AccountsData { get; set; }
    [JsonPropertyName("aspsp")] public Aspsp? Aspsp { get; set; }
    [JsonPropertyName("psu_type")] public string? PsuType { get; set; }
    [JsonPropertyName("psu_id_hash")] public string? PsuIdHash { get; set; }
    [JsonPropertyName("access")] public Access? Access { get; set; }
    [JsonPropertyName("created")] public DateTimeOffset? Created { get; set; }
    [JsonPropertyName("authorized")] public DateTimeOffset? Authorized { get; set; }
    [JsonPropertyName("closed")] public DateTimeOffset? Closed { get; set; }
}

public class AccountData
{
    [JsonPropertyName("uid")] public string? Uid { get; set; }
    [JsonPropertyName("identification_hash")] public string? IdentificationHash { get; set; }
    [JsonPropertyName("identification_hashes")] public string[]? IdentificationHashes { get; set; }
}

public class Aspsp
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
}

public class Access
{
    // Offset-aware so the UTC instant survives regardless of server time zone.
    [JsonPropertyName("valid_until")] public DateTimeOffset? ValidUntil { get; set; }
    [JsonPropertyName("accounts")] public Account[]? Accounts { get; set; }
    [JsonPropertyName("balances")] public bool? Balances { get; set; }
    [JsonPropertyName("transactions")] public bool? Transactions { get; set; }
}

public class Other
{
    [JsonPropertyName("identification")] public string? Identification { get; set; }
    [JsonPropertyName("scheme_name")] public string? SchemeName { get; set; }
    [JsonPropertyName("issuer")] public string? Issuer { get; set; }
}
