using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package2.Clients;
using ITMartin.Media.Application.Pipelines.Package2.Orchestration;
using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Application.Services.Steps.NormalizationStep;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Runtime.HostedServices;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class FileSorterWorkerDependencyInjection
{
    public static IServiceCollection AddFileSorterWorker(
        this IServiceCollection services)
    {
        services.AddScoped<
            Package2WorkflowFactory>();

        services.AddScoped<
            Package1WorkflowRunner>();
        services.AddScoped<
            Package2WorkflowRunner>();
        services.AddScoped<
            Package2WorkflowOrchestrator>();
      
        services.AddScoped<
            IPackage2Client,
            Package2Client>();

        services.AddScoped<
            IImageConverterService,
            ImageConverterService>();

        return services;
    }
}