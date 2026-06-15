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
        services.AddSingleton<
            IMagicCardRecognitionService,
            ClaudeMagicCardRecognitionService>();

        services.AddSingleton<
            IImageAnalysisService,
            ClaudeImageAnalysisService>();

        services.AddSingleton<
            IOpenAiLibraryShelfRecognitionService,
            ClaudeLibraryShelfRecognitionService>();

        services.AddSingleton<
            IReceiptExtractionService,
            ClaudeReceiptExtractionService>();

        return services;
    }
}
