using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BS2.Application.Sync;

public static class SyncServiceCollectionExtensions
{
    /// <summary>
    /// Sync, scheduled sync and expiry notification services. Requires bound <c>SyncOptions</c>,
    /// <c>AppOptions</c> and <c>ClassificationOptions</c> (bind them in the host; BS2.Application
    /// has no configuration binder reference). The worker may replace <see cref="ISyncSchedule"/>.
    /// </summary>
    public static IServiceCollection AddSync(this IServiceCollection services)
    {
        services.TryAddSingleton<ISyncSchedule, NoSyncSchedule>();
        services.AddScoped<SyncService>();
        services.AddScoped<SyncAllUsersService>();
        services.AddScoped<ExpiryNotificationService>();
        return services;
    }
}
