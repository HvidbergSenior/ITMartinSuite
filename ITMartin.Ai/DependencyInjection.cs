using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartin.Ai;

public static class DependencyInjection
{
    public static IServiceCollection AddAi(
        this IServiceCollection services)
    {
        services.AddSingleton<
            IMagazineIdentificationService,
            ClaudeMagazineIdentificationService>();

        services.AddSingleton<
            IMagicCardRecognitionService,
            ClaudeMagicCardRecognitionService>();

        services.AddSingleton<
            IImageAnalysisService,
            ClaudeImageAnalysisService>();

        services.AddSingleton<
            ILibraryShelfRecognitionService,
            ClaudeLibraryShelfRecognitionService>();

        services.AddSingleton<
            ICdRecognitionService,
            ClaudeCdRecognitionService>();

        services.AddSingleton<
            IReceiptExtractionService,
            ClaudeReceiptExtractionService>();

        services.AddSingleton<
            IElBillExtractionService,
            ClaudeElBillExtractionService>();

        services.AddSingleton<
            IAuthorSimilarityService,
            ClaudeAuthorSimilarityService>();

        services.AddSingleton<
            IFaceRecognitionService,
            FaceOnnxRecognitionService>();

        // FaceOnnxRecognitionService serializes every call behind an internal
        // lock (its ONNX sessions aren't confirmed thread-safe for concurrent
        // Forward() calls), so bulk parallel indexing needs its own independent
        // instances rather than sharing the one singleton above.
        services.AddSingleton<Func<IFaceRecognitionService>>(sp =>
            () => new FaceOnnxRecognitionService(sp.GetRequiredService<ILogger<FaceOnnxRecognitionService>>()));

        return services;
    }
}
