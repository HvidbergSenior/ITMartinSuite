using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.QuickSort.Clients;
using ITMartin.Media.Application.Pipelines.QuickSort.Services;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Clients;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;
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
            QuickSortManifestSummaryService>();

        services.AddScoped<
            AnalogDigitizeProfileBuilder>();

        services.AddScoped<
            IQuickSortClient,
            QuickSortClient>();

        services.AddScoped<
            IAnalogDigitizeClient,
            AnalogDigitizeClient>();

        // Vlog Studio's video-enhancement editor (ColorGrade/Deflicker/
        // Stabilization/etc.) is a separate app's own pipeline - it used to
        // be wired up here too (leftover from before that separation) but
        // FileSorter never had a real use for it. See
        // ITMartinVlog.Server/Services/VlogEditorService.cs for its real home.

        services.AddScoped<
            IImageConverterService,
            ImageConverterService>();

        return services;
    }
}