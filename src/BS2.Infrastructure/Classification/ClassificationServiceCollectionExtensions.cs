using Betalgo.Ranul.OpenAI.Extensions;
using BS2.Application;
using BS2.Application.Abstractions;
using BS2.Application.Classification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BS2.Infrastructure.Classification;

public static class ClassificationServiceCollectionExtensions
{
    /// <summary>OpenAI classifier and the service that records runs. Betalgo reads its API key from <c>OpenAIServiceOptions:ApiKey</c>.</summary>
    public static IServiceCollection AddClassification(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ClassificationOptions>(configuration.GetSection(ClassificationOptions.Section));
        services.AddOpenAIService();
        services.AddScoped<ITransactionClassifier, OpenAiTransactionClassifier>();
        services.AddScoped<ITransactionClassificationService, TransactionClassificationService>();
        return services;
    }
}
