using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Services;
using ITMartin.OCR.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Ai;

public static class DependencyInjection
{
    public static IServiceCollection
        AddAi(
            this IServiceCollection services)
    {
        services.AddSingleton<
            IMagicCardRecognitionService,
            OpenAiMagicCardRecognitionService>();

        services.AddSingleton<
            ICardConditionAnalysisService,
            OpenAiCardConditionService>();

        return services;
    }
}