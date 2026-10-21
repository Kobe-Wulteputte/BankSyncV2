using System.Text.Json.Serialization;
using BS2.Infrastructure.EnableBanking.Models.Sessions;

namespace BS2.Infrastructure.EnableBanking.Models.Accounts;

public class GetDetailsRequest
{
    public string? AccountId { get; set; }
}

public class GetDetailsResponse
{
    [JsonPropertyName("cash_account_type")] public string? CashAccountType { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("identification_hash")] public string? IdentificationHash { get; set; }
    [JsonPropertyName("identification_hashes")] public string[]? IdentificationHashes { get; set; }
    [JsonPropertyName("account_id")] public AccountId? AccountId { get; set; }
    [JsonPropertyName("all_account_ids")] public AllAccountId[]? AllAccountIds { get; set; }
    [JsonPropertyName("account_servicer")] public AccountServicer? AccountServicer { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("details")] public string? Details { get; set; }
    [JsonPropertyName("usage")] public string? Usage { get; set; }
    [JsonPropertyName("product")] public string? Product { get; set; }
    [JsonPropertyName("psu_status")] public string? PsuStatus { get; set; }
    [JsonPropertyName("credit_limit")] public CreditLimit? CreditLimit { get; set; }
    [JsonPropertyName("legal_age")] public string? LegalAge { get; set; }
    [JsonPropertyName("uid")] public string? Uid { get; set; }
}

public class AllAccountId
{
    [JsonPropertyName("identification")] public string? Identification { get; set; }
    [JsonPropertyName("scheme_name")] public string? SchemeName { get; set; }
    [JsonPropertyName("issuer")] public string? Issuer { get; set; }
}

public class GetTransactionsRequest
{
    public string? AccountId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? ContinuationKey { get; set; }
    public string? TransactionStatus { get; set; }
    public object? Strategy { get; set; }
}

public class GetTransactionsResponse
{
    [JsonPropertyName("transactions")] public Transaction[]? Transactions { get; set; }

    // V1 fix: upstream bound "continuationKey", the API sends continuation_key.
    [JsonPropertyName("continuation_key")] public string? ContinuationKey { get; set; }
}

public class Transaction
{
    [JsonPropertyName("transaction_amount")] public AmountDetails? TransactionAmount { get; set; }
    [JsonPropertyName("credit_debit_indicator")] public string? CreditDebitIndicator { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("entry_reference")] public string? EntryReference { get; set; }
    [JsonPropertyName("merchant_category_code")] public string? MerchantCategoryCode { get; set; }
    [JsonPropertyName("creditor")] public PartyDetails? Creditor { get; set; }
    [JsonPropertyName("creditor_account")] public AccountDetails? CreditorAccount { get; set; }
    [JsonPropertyName("creditor_agent")] public AgentDetails? CreditorAgent { get; set; }
    [JsonPropertyName("debtor")] public PartyDetails? Debtor { get; set; }
    [JsonPropertyName("debtor_account")] public AccountDetails? DebtorAccount { get; set; }
    [JsonPropertyName("debtor_agent")] public AgentDetails? DebtorAgent { get; set; }
    [JsonPropertyName("bank_transaction_code")] public BankTransactionCode? BankTransactionCode { get; set; }
    [JsonPropertyName("proprietaryBankTransactionCode")] public string? ProprietaryBankTransactionCode { get; set; }
    [JsonPropertyName("booking_date")] public string? BookingDate { get; set; }
    [JsonPropertyName("value_date")] public string? ValueDate { get; set; }
    [JsonPropertyName("transactionInformation")] public string? TransactionInformation { get; set; }
    [JsonPropertyName("transaction_date")] public string? TransactionDate { get; set; }
    [JsonPropertyName("balance_after_transaction")] public AmountDetails? BalanceAfterTransaction { get; set; }
    [JsonPropertyName("balance")] public TransactionBalance? Balance { get; set; }
    [JsonPropertyName("reference_number")] public string? ReferenceNumber { get; set; }
    [JsonPropertyName("remittance_information")] public string[]? RemittanceInformation { get; set; }
    [JsonPropertyName("debtor_account_additional_identification")] public IdentificationDetails[]? DebtorAccountAdditionalIdentification { get; set; }
    [JsonPropertyName("creditor_account_additional_identification")] public IdentificationDetails[]? CreditorAccountAdditionalIdentification { get; set; }
    [JsonPropertyName("exchange_rate")] public ExchangeRateDetails? ExchangeRate { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("transaction_id")] public string? TransactionId { get; set; }
}

public class AmountDetails
{
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("amount")] public string? Amount { get; set; }
}

public class PartyDetails
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("postal_address")] public PostalAddress? PostalAddress { get; set; }
    [JsonPropertyName("organisation_id")] public IdentificationDetails? OrganisationId { get; set; }
    [JsonPropertyName("private_id")] public IdentificationDetails? PrivateId { get; set; }
    [JsonPropertyName("contact_details")] public ContactDetails? ContactDetails { get; set; }
}

public class PostalAddress
{
    [JsonPropertyName("address_type")] public string? AddressType { get; set; }
    [JsonPropertyName("department")] public string? Department { get; set; }
    [JsonPropertyName("sub_department")] public string? SubDepartment { get; set; }
    [JsonPropertyName("street_name")] public string? StreetName { get; set; }
    [JsonPropertyName("building_number")] public string? BuildingNumber { get; set; }
    [JsonPropertyName("post_code")] public string? PostCode { get; set; }
    [JsonPropertyName("town_name")] public string? TownName { get; set; }
    [JsonPropertyName("country_sub_division")] public string? CountrySubDivision { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
    [JsonPropertyName("address_line")] public string[]? AddressLine { get; set; }
}

public class IdentificationDetails
{
    [JsonPropertyName("identification")] public string? Identification { get; set; }
    [JsonPropertyName("scheme_name")] public string? SchemeName { get; set; }
    [JsonPropertyName("issuer")] public string? Issuer { get; set; }
}

public class ContactDetails
{
    [JsonPropertyName("email_address")] public string? EmailAddress { get; set; }
    [JsonPropertyName("phone_number")] public string? PhoneNumber { get; set; }
}

public class AccountDetails
{
    [JsonPropertyName("iban")] public string? Iban { get; set; }
    [JsonPropertyName("other")] public IdentificationDetails? Other { get; set; }
}

public class AgentDetails
{
    [JsonPropertyName("bic_fi")] public string? BicFi { get; set; }
    [JsonPropertyName("clearing_system_member_id")] public ClearingSystemMemberId? ClearingSystemMemberId { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public class BankTransactionCode
{
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("sub_code")] public string? SubCode { get; set; }
}

public class ExchangeRateDetails
{
    [JsonPropertyName("unit_currency")] public string? UnitCurrency { get; set; }
    [JsonPropertyName("exchange_rate")] public string? ExchangeRate { get; set; }
    [JsonPropertyName("rate_type")] public string? RateType { get; set; }
    [JsonPropertyName("contract_identification")] public string? ContractIdentification { get; set; }
    [JsonPropertyName("instructed_amount")] public AmountDetails? InstructedAmount { get; set; }
}

public class TransactionAmount
{
    [JsonPropertyName("amount")] public string? Amount { get; set; }
    [JsonPropertyName("currencyCode")] public string? CurrencyCode { get; set; }
}

public class TransactionBalance
{
    [JsonPropertyName("amount")] public TransactionAmount? Amount { get; set; }
    [JsonPropertyName("creditDebitIndicator")] public string? CreditDebitIndicator { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
}
