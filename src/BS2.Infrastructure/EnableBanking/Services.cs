using System.Security.Cryptography;
using System.Text;
using BS2.Application.Abstractions;
using System.Text.Json;
using BS2.Infrastructure.EnableBanking.Models;
using BS2.Infrastructure.EnableBanking.Models.Accounts;
using BS2.Infrastructure.EnableBanking.Models.General;
using BS2.Infrastructure.EnableBanking.Models.Sessions;

namespace BS2.Infrastructure.EnableBanking;

public abstract class HttpClientService(HttpClient httpClient)
{
    protected Task<ApiResponse<T>> GetAsync<T>(string requestUri, CancellationToken cancellationToken) =>
        SendAsync<T>(requestUri, async () => await httpClient.GetAsync(requestUri, cancellationToken), cancellationToken);

    protected Task<ApiResponse<T>> PostAsync<T>(string requestUri, object requestBody, CancellationToken cancellationToken) =>
        SendAsync<T>(requestUri, async () =>
        {
            var json = JsonSerializer.Serialize(requestBody, EnableBankingJson.Options);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await httpClient.PostAsync(requestUri, content, cancellationToken);
        }, cancellationToken);

    protected Task<ApiResponse<T>> DeleteAsync<T>(string requestUri, CancellationToken cancellationToken) =>
        SendAsync<T>(requestUri, async () => await httpClient.DeleteAsync(requestUri, cancellationToken), cancellationToken);

    /// <summary>One exception type for callers: transport, parsing and key-loading failures all become <see cref="BankingProviderException"/>.</summary>
    private static async Task<ApiResponse<T>> SendAsync<T>(string requestUri, Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var responseMessage = await send();
            return await HandleResponse<T>(responseMessage, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new BankingProviderException($"Could not reach Enable Banking ({requestUri}): {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BankingProviderException($"Enable Banking request timed out ({requestUri}).", ex);
        }
        catch (JsonException ex)
        {
            throw new BankingProviderException($"Enable Banking returned an unreadable response ({requestUri}): {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is InvalidOperationException or CryptographicException or IOException or UnauthorizedAccessException)
        {
            throw new BankingProviderException($"Could not sign the Enable Banking request (check the private key configuration): {ex.Message}", ex);
        }
    }

    private static async Task<ApiResponse<T>> HandleResponse<T>(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
    {
        var response = new ApiResponse<T> { StatusCode = responseMessage.StatusCode };
        var content = await responseMessage.Content.ReadAsStringAsync(cancellationToken);

        if (responseMessage.IsSuccessStatusCode)
        {
            response.Data = JsonSerializer.Deserialize<T>(content, EnableBankingJson.Options);
            return response;
        }

        try
        {
            response.Error = JsonSerializer.Deserialize<ApiError>(content, EnableBankingJson.Options);
        }
        catch (JsonException)
        {
            // Gateways answer with HTML; keep the body so the failure is still diagnosable.
        }

        response.Error ??= new ApiError { Message = $"HTTP {(int)responseMessage.StatusCode}: {content}" };
        return response;
    }
}

public class GeneralService(HttpClient httpClient) : HttpClientService(httpClient), IGeneralService
{
    public Task<ApiResponse<StartAuthorizationResponse>> StartAuthorizationAsync(StartAuthorizationRequest request, CancellationToken cancellationToken) =>
        PostAsync<StartAuthorizationResponse>("auth", request, cancellationToken);

    public Task<ApiResponse<GetASPSPsResponse>> GetASPSPsAsync(GetASPSPsRequest request, CancellationToken cancellationToken)
    {
        var requestUri = "aspsps?";
        if (!string.IsNullOrEmpty(request.Country)) requestUri += $"country={Uri.EscapeDataString(request.Country)}&";
        if (!string.IsNullOrEmpty(request.PsuType)) requestUri += $"psu_type={Uri.EscapeDataString(request.PsuType)}&";
        if (!string.IsNullOrEmpty(request.Service)) requestUri += $"service={Uri.EscapeDataString(request.Service)}&";
        if (!string.IsNullOrEmpty(request.PaymentType)) requestUri += $"payment_type={Uri.EscapeDataString(request.PaymentType)}&";
        return GetAsync<GetASPSPsResponse>(requestUri, cancellationToken);
    }

    public Task<ApiResponse<GetApplicationResponse>> GetApplicationAsync(CancellationToken cancellationToken) =>
        GetAsync<GetApplicationResponse>("application", cancellationToken);
}

public class SessionsService(HttpClient httpClient) : HttpClientService(httpClient), ISessionsService
{
    public Task<ApiResponse<GetSessionResponse>> GetSessionAsync(string sessionId, CancellationToken cancellationToken) =>
        GetAsync<GetSessionResponse>($"sessions/{Uri.EscapeDataString(sessionId)}", cancellationToken);

    public Task<ApiResponse<DeleteSessionResponse>> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken) =>
        DeleteAsync<DeleteSessionResponse>($"sessions/{Uri.EscapeDataString(sessionId)}", cancellationToken);

    public Task<ApiResponse<AuthorizeSessionResponse>> AuthorizeSessionAsync(AuthorizeSessionRequest request, CancellationToken cancellationToken) =>
        PostAsync<AuthorizeSessionResponse>("sessions", request, cancellationToken);
}

public class AccountsService(HttpClient httpClient) : HttpClientService(httpClient), IAccountsService
{
    public Task<ApiResponse<GetTransactionsResponse>> GetTransactionsAsync(GetTransactionsRequest request, CancellationToken cancellationToken)
    {
        var requestUri = $"accounts/{Uri.EscapeDataString(request.AccountId ?? "")}/transactions?";
        if (request.DateFrom != null) requestUri += $"date_from={request.DateFrom.Value:yyyy-MM-dd}&";
        if (request.DateTo != null) requestUri += $"date_to={request.DateTo.Value:yyyy-MM-dd}&";
        if (!string.IsNullOrEmpty(request.ContinuationKey)) requestUri += $"continuation_key={Uri.EscapeDataString(request.ContinuationKey)}&";
        if (!string.IsNullOrEmpty(request.TransactionStatus)) requestUri += $"transaction_status={request.TransactionStatus}&";
        if (request.Strategy != null) requestUri += $"strategy={request.Strategy}&";
        return GetAsync<GetTransactionsResponse>(requestUri, cancellationToken);
    }

    public Task<ApiResponse<GetDetailsResponse>> GetDetailsAsync(GetDetailsRequest request, CancellationToken cancellationToken) =>
        GetAsync<GetDetailsResponse>($"accounts/{Uri.EscapeDataString(request.AccountId ?? "")}/details", cancellationToken);
}
