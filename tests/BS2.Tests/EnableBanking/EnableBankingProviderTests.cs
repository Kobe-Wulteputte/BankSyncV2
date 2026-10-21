using System.Text.Json;
using BS2.Application.Abstractions;
using BS2.Domain.Entities;
using BS2.Infrastructure.EnableBanking;
using BS2.Infrastructure.EnableBanking.Models;
using BS2.Infrastructure.EnableBanking.Models.Accounts;
using Transaction = BS2.Infrastructure.EnableBanking.Models.Accounts.Transaction;
using BS2.Infrastructure.EnableBanking.Models.General;
using BS2.Infrastructure.EnableBanking.Models.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BS2.Tests.EnableBanking;

public class EnableBankingProviderTests
{
    private static EnableBankingProvider Provider(IGeneralService? general = null, IAccountsService? accounts = null) =>
        new(general ?? new FakeGeneralService(), new FakeSessionsService(), accounts ?? new FakeAccountsService(), NullLogger<EnableBankingProvider>.Instance);

    private static BankConnection Connection(params string[] ibans) => new()
    {
        BankName = "Revolut",
        Country = "BE",
        PsuType = "personal",
        ConfiguredIbans = ibans
    };

    [Fact]
    public async Task StartAuthorization_lists_configured_ibans()
    {
        var general = new FakeGeneralService();

        var url = await Provider(general).StartAuthorizationAsync(Connection("BE1", "BE2"), "state-1", new Uri("https://app/callback"), 90, CancellationToken.None);

        Assert.Equal("https://bank/auth", url);
        var request = general.LastRequest!;
        Assert.Equal(["BE1", "BE2"], request.Access!.Accounts!.Select(a => a.Iban));
        Assert.True(request.Access.Balances);
        Assert.True(request.Access.Transactions);
        Assert.True(request.CredentialsAutosubmit);
        Assert.Equal("state-1", request.State);
        Assert.Equal("personal", request.PsuType);
        Assert.Equal("Revolut", request.Aspsp!.Name);
        Assert.Equal("BE", request.Aspsp.Country);
        Assert.InRange(request.Access.ValidUntil!.Value, DateTime.UtcNow.AddDays(89), DateTime.UtcNow.AddDays(91));

        var json = JsonSerializer.Serialize(request, EnableBankingJson.Options);
        Assert.Contains("\"accounts\":[{\"iban\":\"BE1\"},{\"iban\":\"BE2\"}]", json);
        Assert.DoesNotContain("auth_methods", json);
    }

    [Fact]
    public async Task StartAuthorization_omits_accounts_when_selecting_at_bank()
    {
        var general = new FakeGeneralService();
        var connection = Connection("BE1");
        connection.SelectAccountsAtBank = true;

        await Provider(general).StartAuthorizationAsync(connection, "s", new Uri("https://app/callback"), 90, CancellationToken.None);

        Assert.Null(general.LastRequest!.Access!.Accounts);
        Assert.DoesNotContain("\"accounts\"", JsonSerializer.Serialize(general.LastRequest, EnableBankingJson.Options));
    }

    [Fact]
    public async Task StartAuthorization_omits_accounts_when_syncing_all()
    {
        var general = new FakeGeneralService();

        await Provider(general).StartAuthorizationAsync(Connection(), "s", new Uri("https://app/callback"), 90, CancellationToken.None);

        Assert.Null(general.LastRequest!.Access!.Accounts);
    }

    [Fact]
    public async Task StartAuthorization_throws_on_api_error()
    {
        var general = new FakeGeneralService { Error = new ApiError { Message = "ASPSP unknown" } };

        var ex = await Assert.ThrowsAsync<BankingProviderException>(() =>
            Provider(general).StartAuthorizationAsync(Connection(), "s", new Uri("https://app/callback"), 90, CancellationToken.None));

        Assert.Contains("ASPSP unknown", ex.Message);
    }

    [Fact]
    public async Task GetTransactions_follows_continuation_keys_until_null()
    {
        var accounts = new FakeAccountsService(Page("k1", 2), Page("k2", 1), Page(null, 1));

        var result = await Provider(accounts: accounts).GetTransactionsAsync("acc", new DateOnly(2026, 1, 1), CancellationToken.None);

        Assert.Equal(4, result.Count);
        Assert.Equal([null, "k1", "k2"], accounts.RequestedKeys);
    }

    [Fact]
    public async Task GetTransactions_stops_when_continuation_key_does_not_advance()
    {
        var accounts = new FakeAccountsService(Page("same", 1), Page("same", 1), Page("same", 1));

        var result = await Provider(accounts: accounts).GetTransactionsAsync("acc", new DateOnly(2026, 1, 1), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal([null, "same"], accounts.RequestedKeys);
    }

    private static GetTransactionsResponse Page(string? key, int count) => new()
    {
        ContinuationKey = key,
        Transactions = Enumerable.Range(0, count).Select(i => new Transaction
        {
            TransactionAmount = new AmountDetails { Amount = "1.00" },
            CreditDebitIndicator = "DBIT",
            EntryReference = $"{key}-{i}"
        }).ToArray()
    };

    private sealed class FakeGeneralService : IGeneralService
    {
        public StartAuthorizationRequest? LastRequest { get; private set; }
        public ApiError? Error { get; init; }

        public Task<ApiResponse<StartAuthorizationResponse>> StartAuthorizationAsync(StartAuthorizationRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new ApiResponse<StartAuthorizationResponse>
            {
                Error = Error,
                Data = Error == null ? new StartAuthorizationResponse { Url = new Uri("https://bank/auth") } : null
            });
        }

        public Task<ApiResponse<GetASPSPsResponse>> GetASPSPsAsync(GetASPSPsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<GetApplicationResponse>> GetApplicationAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeSessionsService : ISessionsService
    {
        public Task<ApiResponse<GetSessionResponse>> GetSessionAsync(string sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<DeleteSessionResponse>> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<AuthorizeSessionResponse>> AuthorizeSessionAsync(AuthorizeSessionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeAccountsService(params GetTransactionsResponse[] pages) : IAccountsService
    {
        private int _index;
        public List<string?> RequestedKeys { get; } = [];

        public Task<ApiResponse<GetTransactionsResponse>> GetTransactionsAsync(GetTransactionsRequest request, CancellationToken cancellationToken)
        {
            RequestedKeys.Add(request.ContinuationKey);
            if (_index >= pages.Length) throw new InvalidOperationException("More pages requested than provided; pagination did not terminate.");
            return Task.FromResult(new ApiResponse<GetTransactionsResponse> { Data = pages[_index++] });
        }

        public Task<ApiResponse<GetDetailsResponse>> GetDetailsAsync(GetDetailsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

public class HttpClientServiceTests
{
    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw ex;
    }

    private static GeneralService Service(Exception ex) =>
        new(new HttpClient(new ThrowingHandler(ex)) { BaseAddress = new Uri("https://api.example/") });

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(System.Security.Cryptography.CryptographicException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task Transport_and_key_failures_surface_as_banking_provider_exception(Type type)
    {
        var ex = (Exception)Activator.CreateInstance(type)!;

        await Assert.ThrowsAsync<BankingProviderException>(() => Service(ex).GetApplicationAsync(CancellationToken.None));
    }
}
