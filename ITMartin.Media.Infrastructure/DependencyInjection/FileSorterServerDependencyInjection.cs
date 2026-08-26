using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1.Clients;
using ITMartin.Media.Application.Pipelines.Package1.Services;
using ITMartin.Media.Application.Pipelines.Package2.Clients;
using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Application.Pipelines.Package4.Clients;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Application.Services.Steps.DuplicationStep;
using ITMartin.Media.Application.Services.Steps.ExportStep;
using ITMartin.Media.Application.Services.Steps.NormalizationStep;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.BackgroundJobs;
using ITMartin.Media.Infrastructure.Events;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Media;
using ITMartin.Media.Infrastructure.Persistence.Repositories;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.Media.Infrastructure.SignalR.Runtime;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class FileSorterServerDependencyInjection
{
    public static IServiceCollection AddFileSorterServer(
        this IServiceCollection services)
    {
        services.AddSingleton<
            IBackgroundJobQueue,
            RabbitMqBackgroundJobQueue>();

        services.AddSingleton<
            IOcrService,
            OcrService>();

        services.AddScoped<
            ProgressService>();

        services.AddScoped<
            Package1ManifestSummaryService>();

        services.AddScoped<
            Package2ProfileBuilder>();

        services.AddScoped<
            IPackage1Client,
            Package1Client>();

        services.AddScoped<
            IPackage2Client,
            Package2Client>();

        services.AddScoped<
            IPackage4Client,
            Package4Client>();

        services.AddScoped<
            IImageConverterService,
            ImageConverterService>();

        return services;
    }
}