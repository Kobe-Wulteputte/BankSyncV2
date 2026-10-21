using BS2.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BS2.Infrastructure.EnableBanking;

public static class EnableBankingServiceCollectionExtensions
{
    private const string BaseAddress = "https://api.enablebanking.com/";

    public static IServiceCollection AddEnableBanking(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EnableBankingOptions>(configuration.GetSection(EnableBankingOptions.Section));
        services.AddTransient<TokenHandler>();
        services.AddTransient<PsuHeaderHandler>();

        AddClient<IGeneralService, GeneralService>(services);
        AddClient<ISessionsService, SessionsService>(services);
        AddClient<IAccountsService, AccountsService>(services);

        services.AddScoped<IBankingProvider, EnableBankingProvider>();
        return services;
    }

    private static void AddClient<TInterface, TImplementation>(IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface =>
        services.AddHttpClient<TInterface, TImplementation>(client => client.BaseAddress = new Uri(BaseAddress))
            .AddHttpMessageHandler<TokenHandler>()
            .AddHttpMessageHandler<PsuHeaderHandler>();
}
