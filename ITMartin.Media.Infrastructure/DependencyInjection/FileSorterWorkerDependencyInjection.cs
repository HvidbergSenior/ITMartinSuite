using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Clients;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Orchestration;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;
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
            AnalogDigitizeWorkflowFactory>();

        services.AddScoped<
            QuickSortWorkflowRunner>();

        services.AddScoped<
            AnalogDigitizeWorkflowRunner>();

        services.AddScoped<
            IAnalogDigitizeClient,
            AnalogDigitizeClient>();

        services.AddScoped<
            IImageConverterService,
            ImageConverterService>();

        return services;
    }
}