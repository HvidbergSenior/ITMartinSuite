using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Ai;

public static class DependencyInjection
{
    public static IServiceCollection AddAi(
        this IServiceCollection services)
    {
        services.AddScoped<
            IMagicCardRecognitionService,
            OpenAiMagicCardRecognitionService>();

        services.AddScoped<
            ICardConditionAnalysisService,
            OpenAiCardConditionService>();

        services.AddScoped<
            IImageAnalysisService,
            OpenOpenAiAnalysisService>();
        services.AddScoped<
            IOpenAiLibraryShelfRecognitionService,
            OpenAiLibraryShelfRecognitionService>();
        services.AddScoped<
            IReceiptExtractionService,
            OpenAiReceiptExtractionService>();

        return services;
    }
}