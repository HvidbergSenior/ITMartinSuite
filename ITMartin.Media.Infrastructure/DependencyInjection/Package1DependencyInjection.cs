using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Services;
using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Runtime.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class Package1DependencyInjection
{
    public static IServiceCollection AddPackage1Pipeline(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<
            IScanOrchestrator,
            Package1WorkflowOrchestrator>();

        services.AddScoped<
            Package1WorkflowDefinition>();

        services.AddScoped<
            Package1WorkflowOrchestrator>();

        services.AddScoped<
            Package1ExportService>();

        services.AddScoped<
            Package1ManifestBuilder>();

        services.AddScoped<
            Package1ManifestWriter>();

        services.AddScoped<
            Package1ManifestLoader>();

        services.AddScoped<
            Package1CleanupResultBuilder>();

        services.AddScoped<
            FileDiscoveryWorkflowStep>();

        services.AddScoped<
            VideoSegmentationWorkflowStep>();

        services.AddScoped<
            HashWorkflowStep>();

        services.AddScoped<
            SegmentThumbnailWorkflowStep>();

        services.AddScoped<
            MetadataWorkflowStep>();

        services.AddScoped<
            ImageNormalizationWorkflowStep>();

        services.AddScoped<
            MediaRulesWorkflowStep>();

        services.AddScoped<
            VideoNormalizationWorkflowStep>();

        services.AddScoped<
            ThumbnailWorkflowStep>();

        services.AddScoped<
            DuplicateDetectionWorkflowStep>();

        services.AddScoped<
            CleanupEvaluationWorkflowStep>();

        services.AddScoped<
            Manifest1BuildWorkflowStep>();

        services.AddScoped<
            ExportWorkflowExecutionStep>();
        services.AddScoped<
            IBackgroundJobHandler,
            StartPackage1Handler>();

        return services;
    }
}