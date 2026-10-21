using Microsoft.Extensions.DependencyInjection;

namespace BS2.Application.Banking;

public static class BankingServiceCollectionExtensions
{
    /// <summary>Bank connection, account and authorization-flow services. Requires <c>IBankingProvider</c>, <c>IAppDbContext</c>, <c>ICurrentUser</c> and bound <c>SyncOptions</c>/<c>AppOptions</c>.</summary>
    public static IServiceCollection AddBanking(this IServiceCollection services)
    {
        services.AddScoped<AuthorizationFlowService>();
        services.AddScoped<BankConnectionService>();
        services.AddScoped<AccountService>();
        return services;
    }
}
