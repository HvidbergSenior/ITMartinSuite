using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Application.Services.Steps.DuplicationStep;
using ITMartin.Media.Application.Services.Steps.ExportStep;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.Collections;
using ITMartin.Media.Infrastructure.Events;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Media;
using ITMartin.Media.Infrastructure.Persistence.Repositories;
using ITMartin.Media.Infrastructure.Pipelines.Package3;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.Media.Infrastructure.SignalR.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class FileSorterDependencyInjection
{
    public static IServiceCollection AddFileSorterCore(
        this IServiceCollection services)
    {
        services.AddScoped<
            IMediaTypeResolver,
            MediaTypeResolver>();

        services.AddScoped<
            IThumbnailService,
            ThumbnailService>();

        services.AddScoped<
            IDuplicateService,
            DuplicateService>();

        services.AddScoped<
            ILibraryExportService,
            LibraryExportService>();

        services.AddScoped<
            IMediaNamingService,
            MediaNamingService>();

        services.AddScoped<
            IScanSessionRepository,
            ScanSessionRepository>();

        services.AddScoped<
            ILibraryPathProvider,
            LibraryPathProvider>();

        services.AddSingleton<
            ICollectionStore,
            JsonCollectionStore>();

        services.AddScoped<
            IPackage3Service,
            Package3Service>();

        services.AddScoped<
            ISmartFoldersService,
            SmartFoldersService>();

        services.AddScoped<
            IImageTaggingService,
            ImageTaggingService>();

        services.AddScoped<
            IStaticGalleryExportService,
            StaticGalleryExportService>();

        services.AddScoped<
            ILibraryPolishService,
            LibraryPolishService>();

        services.AddScoped<
            IGalleryThumbnailService,
            GalleryThumbnailService>();

        services.AddScoped<
            IPackageHistoryService,
            PackageHistoryService>();

        services.AddSingleton<
            IEventPublisher,
            NullEventPublisher>();

        services.AddSingleton<
            IRuntimeEventPublisher,
            NullRuntimeEventPublisher>();

        return services;
    }
}