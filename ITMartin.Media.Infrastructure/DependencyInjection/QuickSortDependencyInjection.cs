using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Application.Pipelines.QuickSort.Services;
using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;
using ITMartin.Media.Runtime.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class QuickSortDependencyInjection
{
    public static IServiceCollection AddQuickSortPipeline(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<
            IScanOrchestrator,
            QuickSortWorkflowOrchestrator>();

        services.AddScoped<
            QuickSortWorkflowDefinition>();

        services.AddScoped<
            QuickSortWorkflowOrchestrator>();

        services.AddScoped<
            QuickSortExportService>();

        services.AddScoped<
            QuickSortManifestBuilder>();

        services.AddScoped<
            QuickSortManifestWriter>();

        services.AddScoped<
            QuickSortManifestLoader>();

        services.AddScoped<
            QuickSortCleanupResultBuilder>();

        services.AddScoped<
            CleanStartWorkflowStep>();

        services.AddScoped<
            DvdJoinWorkflowStep>();

        services.AddScoped<
            FileDiscoveryWorkflowStep>();

        services.AddScoped<
            HashWorkflowStep>();

        services.AddScoped<
            MetadataWorkflowStep>();

        services.AddScoped<
            ImageNormalizationWorkflowStep>();

        services.AddScoped<
            ImageQualityWorkflowStep>();

        services.AddScoped<
            MediaRulesWorkflowStep>();

        services.AddScoped<
            LivePhotoDetectionWorkflowStep>();

        services.AddScoped<
            VideoNormalizationWorkflowStep>();

        services.AddScoped<
            DuplicateDetectionWorkflowStep>();

        services.AddScoped<
            AudioDuplicateDetectionWorkflowStep>();

        services.AddScoped<
            CleanupEvaluationWorkflowStep>();

        services.AddScoped<
            AiClassificationWorkflowStep>();

        services.AddScoped<
            Manifest1BuildWorkflowStep>();

        services.AddScoped<
            ExportWorkflowExecutionStep>();

        services.AddScoped<
            GalleryThumbnailWorkflowStep>();

        services.AddScoped<
            FileStatusWorkflowStep>();

        services.AddScoped<
            IBackgroundJobHandler,
            StartQuickSortHandler>();

        return services;
    }
}