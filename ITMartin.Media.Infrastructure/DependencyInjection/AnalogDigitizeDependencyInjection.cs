using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Orchestration;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
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

public static class AnalogDigitizeDependencyInjection
{
    public static IServiceCollection AddAnalogDigitizePipeline(
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

        services.AddScoped<AnalogDigitizeWorkflowDefinition>();

        services.AddScoped<
            AnalogDigitizeWorkflowFactory>();

        services.AddScoped<
            AnalogDigitizeWorkflowOrchestrator>();

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
            IEnhancedFileNamingService,
            EnhancedFileNamingService>();

        services.AddScoped<
            QuickSortManifestLoader>();

        services.AddScoped<
            AnalogDigitizeManifestBuilder>();

        services.AddScoped<
            IAnalogDigitizeManifestStore,
            EfAnalogDigitizeManifestStore>();

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
            IConcurrentVideoDispatcher,
            ConcurrentVideoDispatcher>();
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
            StartAnalogDigitizeHandler>();
        
       
        return services;
    }
}