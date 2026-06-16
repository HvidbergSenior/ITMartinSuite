using ITMartin.Ai.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Infrastructure.FileSystem;
using ITMartin.Media.Infrastructure.Hashing;
using ITMartin.Media.Infrastructure.Metadata;
using ITMartin.Media.Infrastructure.Options;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using ITMartin.Media.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddMediaInfrastructureCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("MediaDb")
            ?? "Data Source=media.db";
       
        services.AddDbContextFactory<Persistence.MediaDbContext>(
            options =>
            {
                options.UseSqlite(
                    connectionString,
                    builder =>
                    {
                        builder.MigrationsAssembly(
                            typeof(Persistence.MediaDbContext)
                                .Assembly.FullName);
                    });
            });

        services.AddScoped<
            IWorkflowCheckpointStore,
            EfWorkflowCheckpointStore>();

        services.AddScoped<
            IWorkflowInstanceStore,
            EfWorkflowInstanceStore>();

        services.AddScoped<
            IWorkflowStepExecutionStore,
            EfWorkflowStepExecutionStore>();

        services.AddScoped<
            IScanSessionStore,
            EfScanSessionStore>();

        services.AddScoped<
            IPackage1ManifestStore,
            EfPackage1ManifestStore>();

        services.AddScoped<
            IFileSystem,
            FileSystemService>();

        services.AddScoped<
            IFileScanner,
            FileScanner>();

        services.AddScoped<
            IHashService,
            Sha256HashService>();

        services.AddScoped<
            IExifService,
            ExifService>();

        services.AddScoped<
            IGpsService,
            GpsService>();

        services.AddScoped<
            IMediaDateService,
            MediaDateService>();

        services.AddScoped<
            IImageMetadataService,
            ImageMetadataService>();

        services.AddScoped<
            IVideoMetadataService,
            VideoMetadataService>();

        services.AddScoped<
            IDocumentMetadataService,
            DocumentMetadataService>();

        services.AddScoped<
            IAiEnrichmentService,
            AiEnrichmentService>();

        services.AddSingleton<
            IImageAnalysisService,
            ClaudeImageAnalysisService>();

        services.Configure<MediaSettingsOptions>(
            configuration.GetSection("MediaSettings"));

        return services;
    }
}