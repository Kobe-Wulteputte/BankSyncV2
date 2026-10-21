using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using BS2.Infrastructure.EnableBanking.Models.Accounts;
using BS2.Infrastructure.EnableBanking.Models.General;
using BS2.Infrastructure.EnableBanking.Models.Sessions;
using Microsoft.Extensions.Logging;

namespace BS2.Infrastructure.EnableBanking;

public sealed class EnableBankingProvider(
    IGeneralService generalService,
    ISessionsService sessionsService,
    IAccountsService accountsService,
    ILogger<EnableBankingProvider> logger) : IBankingProvider
{
    public async Task<IReadOnlyList<ProviderBank>> GetBanksAsync(string country, CancellationToken ct)
    {
        var response = await generalService.GetASPSPsAsync(new GetASPSPsRequest { Country = country }, ct);
        if (response.Error != null)
            throw new BankingProviderException($"Could not list banks for {country}: {response.Error.Message}");

        return (response.Data?.Aspsps ?? [])
            .Where(a => !string.IsNullOrEmpty(a.Name))
            .Select(a => new ProviderBank(
                a.Name!,
                a.Country ?? country,
                a.Logo,
                a.PsuTypes ?? [],
                a.MaximumConsentValidity is { } seconds ? seconds / 86400 : null,
                null))
            .ToList();
    }

    public async Task<string> StartAuthorizationAsync(BankConnection connection, string state, Uri redirectUrl, int consentValidityDays, CancellationToken ct)
    {
        var response = await generalService.StartAuthorizationAsync(new StartAuthorizationRequest
        {
            Access = new Models.General.Access
            {
                ValidUntil = DateTime.UtcNow.AddDays(consentValidityDays),
                Balances = true,
                Transactions = true,
                // Omitted when the ASPSP ignores a pre-specified list (user picks at the bank) and
                // when no IBANs are configured: an empty array would request access to nothing.
                Accounts = connection.SelectAccountsAtBank || connection.SyncsAllAccounts
                    ? null
                    : connection.ConfiguredIbans.Select(iban => new Models.General.Account { Iban = iban }).ToArray()
            },
            CredentialsAutosubmit = true,
            RedirectUrl = redirectUrl,
            State = state,
            PsuType = connection.PsuType,
            Aspsp = new Models.General.Aspsp { Name = connection.BankName, Country = connection.Country }
        }, ct);

        if (response.Error != null || response.Data?.Url == null)
            throw new BankingProviderException($"{connection.BankName}: could not start authorization: {response.Error?.Message ?? "no URL returned"}");

        return response.Data.Url.ToString();
    }

    public async Task<ProviderSession> CompleteAuthorizationAsync(string code, CancellationToken ct)
    {
        var response = await sessionsService.AuthorizeSessionAsync(new AuthorizeSessionRequest { Code = code.Trim() }, ct);
        if (response.Error != null || string.IsNullOrEmpty(response.Data?.SessionId))
            throw new BankingProviderException($"Authorization failed: {response.Error?.Message ?? "no session id returned"}");

        var data = response.Data;
        var accounts = (data.Accounts ?? [])
            .Where(a => !string.IsNullOrEmpty(a.Uid))
            .Select(a => ToProviderAccount(
                a.Uid!,
                a.AccountId?.Iban ?? a.AllAccountIds?.FirstOrDefault(id => id.Iban != null)?.Iban,
                a.AccountId?.Other?.Identification
                ?? a.AllAccountIds?.FirstOrDefault(id => id.Other?.Identification != null)?.Other?.Identification
                ?? a.Name,
                a.Name,
                a.Currency))
            .OfType<ProviderAccount>()
            .ToList();

        return new ProviderSession(
            data.SessionId!,
            data.Aspsp?.Name ?? "",
            data.Aspsp?.Country ?? "",
            data.Access?.ValidUntil?.UtcDateTime,
            accounts);
    }

    public async Task<ProviderSession?> GetSessionAsync(string sessionId, CancellationToken ct)
    {
        var response = await sessionsService.GetSessionAsync(sessionId, ct);
        if (response.Error != null || response.Data == null)
        {
            logger.LogWarning("Session {SessionId} unavailable: {Message}", sessionId, response.Error?.Message ?? "no data returned");
            return null;
        }

        // The bank can end a session before access.valid_until; the API still answers 200 then,
        // only the status shows it (EXPIRED, REVOKED, CLOSED, ...).
        if (response.Data.Status != null && !string.Equals(response.Data.Status, "AUTHORIZED", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Session {SessionId} unavailable: status is {Status}", sessionId, response.Data.Status);
            return null;
        }

        var data = response.Data;
        var accounts = new List<ProviderAccount>();
        foreach (var uid in data.Accounts ?? [])
        {
            // A failed lookup is skipped rather than fatal, like V1's FillMetadataAsync.
            if (await GetAccountAsync(uid, ct) is { } account) accounts.Add(account);
        }

        return new ProviderSession(
            sessionId,
            data.Aspsp?.Name ?? "",
            data.Aspsp?.Country ?? "",
            data.Access?.ValidUntil?.UtcDateTime,
            accounts);
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct)
    {
        var response = await sessionsService.DeleteSessionAsync(sessionId, ct);
        if (response.Error != null)
            throw new BankingProviderException($"Could not delete session {sessionId}: {response.Error.Message}");
    }

    public async Task<ProviderAccount?> GetAccountAsync(string accountId, CancellationToken ct)
    {
        var response = await accountsService.GetDetailsAsync(new GetDetailsRequest { AccountId = accountId }, ct);
        if (response.Error != null || response.Data == null)
        {
            logger.LogWarning("Could not resolve account {AccountId}: {Message}", accountId, response.Error?.Message ?? "no data returned");
            return null;
        }

        var d = response.Data;
        return ToProviderAccount(
            d.Uid ?? accountId,
            d.AccountId?.Iban ?? d.AllAccountIds?.FirstOrDefault(id => id.SchemeName == "IBAN")?.Identification,
            d.AccountId?.Other?.Identification ?? d.AllAccountIds?.FirstOrDefault()?.Identification ?? d.Name,
            d.Name,
            d.Currency);
    }

    public async Task<IReadOnlyList<ProviderTransaction>> GetTransactionsAsync(string accountId, DateOnly from, CancellationToken ct)
    {
        var transactions = new List<ProviderTransaction>();
        string? continuationKey = null;

        do
        {
            var previousKey = continuationKey;
            var page = await accountsService.GetTransactionsAsync(new GetTransactionsRequest
            {
                AccountId = accountId,
                DateFrom = from.ToDateTime(TimeOnly.MinValue),
                ContinuationKey = continuationKey
            }, ct);

            if (page.Error != null)
                throw new BankingProviderException($"Could not fetch transactions for account {accountId}: {page.Error.Message}");

            transactions.AddRange((page.Data?.Transactions ?? []).Select(TransactionMapper.Map));
            continuationKey = page.Data?.ContinuationKey;

            if (continuationKey != null && continuationKey == previousKey)
            {
                logger.LogError("Account {AccountId}: continuation key did not advance; stopping pagination.", accountId);
                break;
            }
        } while (!string.IsNullOrEmpty(continuationKey));

        return transactions;
    }

    /// <summary>
    /// An IBAN is normalized so it compares against configured IBANs; anything else is kept verbatim
    /// as a label (PayPal identifies accounts by email, which normalization would mangle).
    /// </summary>
    private static ProviderAccount? ToProviderAccount(string uid, string? iban, string? fallback, string? name, string? currency)
    {
        var normalized = Iban.Normalize(iban);
        var identifier = normalized.Length > 0 ? normalized : (fallback ?? "").Trim();
        if (identifier.Length == 0) return null;

        return new ProviderAccount(uid, identifier, normalized.Length > 0, name, currency ?? "EUR");
    }
}
