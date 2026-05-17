using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Orchestration;
using ITMartin.Media.Application.Workflow;
using ITMartin.Media.Application.Workflow.Abstractions;
using ITMartin.Media.Application.Workflow.Steps;
using ITMartin.Media.Infrastructure.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class WorkflowServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();

        services.AddScoped<FileDiscoveryWorkflowStep>();

        services.AddScoped<HashWorkflowStep>();

        services.AddScoped<MetadataWorkflowStep>();

        services.AddScoped<Package1WorkflowOrchestrator>();

        services.AddHostedService<ThumbnailWorker>();

        return services;
    }
}