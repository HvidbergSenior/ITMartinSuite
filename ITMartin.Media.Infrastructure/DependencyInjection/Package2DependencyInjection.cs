using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Pipelines.Package2.Orchestration;
using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Application.Services.Steps.NormalizationStep;
using ITMartin.Media.Contracts.Configuration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Media;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using ITMartin.Media.Runtime.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class Package2DependencyInjection
{
    public static IServiceCollection AddPackage2Pipeline(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // =========================
        // CONFIGURATION
        // =========================

        services.Configure<Hi8PipelineOptions>(
            configuration.GetSection("Hi8Pipeline"));

        // =========================
        // WORKFLOW
        // =========================

        services.AddScoped<Package2WorkflowDefinition>();

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
            IVideoSampleService,
            FfmpegVideoSampleService>();
        
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
        // VIDEO STEPS
        // =========================

        services.AddScoped<
            RestorationPreparationWorkflowStep>();
       
        services.AddScoped<
            VideoRenderWorkflowStep>();
        services.AddScoped<
            VideoSampleGenerationWorkflowStep>();
        services.AddScoped<
            VideoDeinterlaceWorkflowStep>();

        services.AddScoped<
            VideoCropWorkflowStep>();

        services.AddScoped<
            VideoStabilizationWorkflowStep>();

        services.AddScoped<
            VideoDenoiseWorkflowStep>();

        services.AddScoped<
            VideoColorCorrectionWorkflowStep>();

        services.AddScoped<
            VideoSharpenWorkflowStep>();

        services.AddScoped<
            VideoUpscaleWorkflowStep>();

        // =========================
        // AUDIO STEPS
        // =========================

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

        // =========================
        // OPTIONAL IMAGE STEPS
        // =========================

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

        // =========================
        // ANALYSIS / FIXUP
        // =========================

        services.AddScoped<
            CropDetectionWorkflowStep>();

        services.AddScoped<
            BorderRemovalWorkflowStep>();

        services.AddScoped<
            AspectRatioCorrectionWorkflowStep>();

        services.AddScoped<
            QualityEvaluationWorkflowStep>();

        // =========================
        // OUTPUT
        // =========================

        services.AddScoped<
            EnhancedThumbnailWorkflowStep>();

        services.AddScoped<
            Manifest2BuildWorkflowStep>();
        services.AddScoped<
            IVideoSegmentThumbnailService,
            VideoSegmentThumbnailService>();
        services.AddScoped<
            IVideoBatchService,
            VideoBatchService>();
        services.AddScoped<
            IVideoConverterService,
            VideoConverterService>();
        services.AddScoped<
            IVideoSegmentationService,
            VideoSegmentationService>();
        services.AddScoped<
            ExportEnhancedAssetsWorkflowStep>();
        services.AddScoped<
            IBackgroundJobHandler,
            StartPackage2Handler>();
        
       
        return services;
    }
}