using BS2.Infrastructure.EnableBanking.Models;
using BS2.Infrastructure.EnableBanking.Models.Accounts;
using BS2.Infrastructure.EnableBanking.Models.General;
using BS2.Infrastructure.EnableBanking.Models.Sessions;

namespace BS2.Infrastructure.EnableBanking;

public interface IGeneralService
{
    Task<ApiResponse<StartAuthorizationResponse>> StartAuthorizationAsync(StartAuthorizationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<GetASPSPsResponse>> GetASPSPsAsync(GetASPSPsRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<GetApplicationResponse>> GetApplicationAsync(CancellationToken cancellationToken);
}

public interface ISessionsService
{
    Task<ApiResponse<GetSessionResponse>> GetSessionAsync(string sessionId, CancellationToken cancellationToken);
    Task<ApiResponse<DeleteSessionResponse>> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken);
    Task<ApiResponse<AuthorizeSessionResponse>> AuthorizeSessionAsync(AuthorizeSessionRequest request, CancellationToken cancellationToken);
}

public interface IAccountsService
{
    Task<ApiResponse<GetTransactionsResponse>> GetTransactionsAsync(GetTransactionsRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<GetDetailsResponse>> GetDetailsAsync(GetDetailsRequest request, CancellationToken cancellationToken);
}
