using ITMartin.Media.Application.Pipelines.Package2.Orchestration;
using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Media;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure;

public static class Package2DependencyInjection
{
    public static IServiceCollection AddPackage2Pipeline(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // =========================
        // WORKFLOW
        // =========================

        services.AddScoped<
            Package2WorkflowDefinition>();

        services.AddScoped<
            IWorkflowDefinition,
            Package2WorkflowDefinition>();

        services.AddScoped<
            Package2WorkflowFactory>();

        services.AddScoped<
            Package2WorkflowOrchestrator>();

        // =========================
        // SERVICES
        // =========================

        services.AddScoped<
            IVideoEnhancementService,
            FfmpegVideoProcessingService>();

        services.AddScoped<
            IAudioEnhancementService,
            FfmpegAudioProcessingService>();

        services.AddScoped<
            IAudioExtractionService,
            FfmpegAudioExtractionService>();

        services.AddScoped<
            IImageEnhancementService,
            ImageProcessingService>();

        services.AddScoped<
            IThumbnailService,
            ThumbnailService>();

        services.AddScoped<
            IEnhancedFileNamingService,
            EnhancedFileNamingService>();

        services.AddScoped<
            Package1ManifestLoader>();

        services.AddScoped<
            Package2ManifestBuilder>();

        services.AddScoped<
            IPackage2ManifestStore,
            EfPackage2ManifestStore>();

        // =========================
        // STEPS
        // =========================

        services.AddScoped<
            RestorationPreparationWorkflowStep>();

        services.AddScoped<
            ImageColorCorrectionWorkflowStep>();

        services.AddScoped<
            ImageContrastWorkflowStep>();

        services.AddScoped<
            ImageDenoiseWorkflowStep>();

        services.AddScoped<
            ImageDeblurWorkflowStep>();

        services.AddScoped<
            ImageUpscaleWorkflowStep>();

        services.AddScoped<
            VideoDeinterlaceWorkflowStep>();

        services.AddScoped<
            VideoStabilizationWorkflowStep>();

        services.AddScoped<
            VideoDenoiseWorkflowStep>();

        services.AddScoped<
            VideoSharpenWorkflowStep>();

        services.AddScoped<
            VideoColorCorrectionWorkflowStep>();

        services.AddScoped<
            VideoUpscaleWorkflowStep>();

        services.AddScoped<
            AudioExtractionWorkflowStep>();

        services.AddScoped<
            AudioNoiseReductionWorkflowStep>();

        services.AddScoped<
            AudioHumRemovalWorkflowStep>();

        services.AddScoped<
            AudioLevelingWorkflowStep>();

        services.AddScoped<
            AudioSpeechEnhancementWorkflowStep>();

        services.AddScoped<
            AudioMuxWorkflowStep>();

        services.AddScoped<
            CropDetectionWorkflowStep>();

        services.AddScoped<
            BorderRemovalWorkflowStep>();

        services.AddScoped<
            AspectRatioCorrectionWorkflowStep>();

        services.AddScoped<
            QualityEvaluationWorkflowStep>();

        services.AddScoped<
            EnhancedThumbnailWorkflowStep>();

        services.AddScoped<
            ManifestBuildWorkflowStep>();

        services.AddScoped<
            ExportEnhancedAssetsWorkflowStep>();

        return services;
    }
}