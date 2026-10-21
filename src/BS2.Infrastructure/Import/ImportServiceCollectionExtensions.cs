using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BS2.Infrastructure.Import;

public static class ImportServiceCollectionExtensions
{
    public static IServiceCollection AddExcelImport(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ImportOptions>(configuration.GetSection(ImportOptions.Section));
        return services.AddScoped<ExcelImportService>();
    }
}
