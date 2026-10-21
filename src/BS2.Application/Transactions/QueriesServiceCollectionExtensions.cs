using BS2.Application.Analytics;
using BS2.Application.Categories;
using BS2.Application.Groups;
using Microsoft.Extensions.DependencyInjection;

namespace BS2.Application.Transactions;

public static class QueriesServiceCollectionExtensions
{
    /// <summary>Transactions, analytics, categories and groups. All scoped; they only need <c>IAppDbContext</c>, <c>ICurrentUser</c>, <c>IClock</c> and <c>ITransactionClassificationService</c>.</summary>
    public static IServiceCollection AddQueries(this IServiceCollection services)
    {
        services.AddScoped<TransactionQueryService>();
        services.AddScoped<TransactionCommandService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<CategoryClassService>();
        services.AddScoped<GroupService>();
        return services;
    }
}
