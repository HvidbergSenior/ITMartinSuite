using ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;
using ITMartin.Media.Application.Pipelines.Package4.Orchestration;
using ITMartin.Media.Application.Pipelines.Package4.Services;
using ITMartin.Media.Application.Pipelines.Package4.Steps;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Runtime.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class Package4DependencyInjection
{
    public static IServiceCollection AddPackage4Pipeline(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // =========================
        // WORKFLOW
        // =========================

        services.AddScoped<Package4WorkflowDefinition>();
        services.AddScoped<Package4WorkflowFactory>();
        services.AddScoped<Package4WorkflowOrchestrator>();
        services.AddScoped<Package4WorkflowRunner>();

        // Shared with AnalogDigitize - not duplicated here.
        services.AddScoped<QuickSortManifestLoader>();

        // =========================
        // STEPS
        // =========================

        services.AddScoped<SocialClipPreparationWorkflowStep>();

        services.AddScoped<WhiteBalanceCorrectionWorkflowStep>();
        services.AddScoped<ExposureContrastCorrectionWorkflowStep>();
        services.AddScoped<SaturationVibranceWorkflowStep>();
        services.AddScoped<ColorGradeWorkflowStep>();
        services.AddScoped<VideoSharpenWorkflowStep>();
        services.AddScoped<VideoNoiseReductionWorkflowStep>();
        services.AddScoped<DeflickerWorkflowStep>();
        services.AddScoped<StabilizationWorkflowStep>();
        services.AddScoped<StabilizedCropWorkflowStep>();

        services.AddScoped<AudioNoiseReductionWorkflowStep>();
        services.AddScoped<WindNoiseReductionWorkflowStep>();
        services.AddScoped<AudioHumRemovalWorkflowStep>();
        services.AddScoped<AudioEqWorkflowStep>();
        services.AddScoped<DeEssWorkflowStep>();
        services.AddScoped<AudioCompressionWorkflowStep>();
        services.AddScoped<LoudnessNormalizationWorkflowStep>();

        services.AddScoped<VideoAudioRenderWorkflowStep>();
        services.AddScoped<TrimDeadFootageWorkflowStep>();
        services.AddScoped<DeliveryExportWorkflowStep>();

        // =========================
        // BACKGROUND JOB HANDLER
        // =========================

        services.AddScoped<IBackgroundJobHandler, StartPackage4Handler>();

        return services;
    }
}
