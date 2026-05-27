using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Services;
using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Application.Services.Steps.NormalizationStep;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.Ai;
using ITMartin.Media.Infrastructure.FileSystem;
using ITMartin.Media.Infrastructure.Hashing;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Media;
using ITMartin.Media.Infrastructure.Metadata;
using ITMartin.Media.Infrastructure.Options;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.Media.Runtime.Execution;
using ITMartin.Media.Runtime.HostedServices;
using ITMartin.Media.Runtime.Recovery;
using ITMartin.Media.Runtime.Registry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITMartin.Media.Infrastructure;

public static class Package1DependencyInjection
{
    public static IServiceCollection AddMediaInfrastructureCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("MediaDb")
            ?? "Data Source=media.db";

        services.AddDbContextFactory<Persistence.MediaDbContext>(options =>
        {
            options.UseSqlite(
                connectionString,
                builder =>
                {
                    builder.MigrationsAssembly(
                        typeof(Persistence.MediaDbContext).Assembly.FullName);
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
            IVideoSegmentationService,
            VideoSegmentationService>();

        services.AddScoped<
            IFileScanner,
            FileScanner>();

        services.AddScoped<
            IMediaClassificationService,
            MediaClassificationService>();

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
            VideoConverterService>();

        services.AddScoped<
            IVideoBatchService,
            VideoBatchService>();
        services.AddScoped<
            IVideoSegmentThumbnailService,
            VideoSegmentThumbnailService>();
        services.AddScoped<
            SubtitleService>();

        services.AddScoped<
            IImageBatchService,
            ImageBatchService>();

        services.AddScoped<
            ImageConverterService>();

        services.AddScoped<
            ThumbnailService>();

        services.AddScoped<
            IAiEnrichmentService,
            AiEnrichmentService>();

        services.AddScoped<
            IImageAnalysisService,
            OpenAiImageAnalysisService>();

        services.Configure<MediaSettingsOptions>(
            configuration.GetSection("MediaSettings"));

        services.Configure<OpenAiOptions>(
            configuration.GetSection("OpenAI"));

        return services;
    }

    public static IServiceCollection AddMediaWorkflowRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<
            IWorkflowExecutor,
            WorkflowExecutor>();

        services.AddScoped<
            IWorkflowRegistry,
            WorkflowRegistry>();

        services.AddScoped<
            IWorkflowRecoveryService,
            WorkflowRecoveryService>();

        services.AddScoped<
            IScanOrchestrator,
            Package1WorkflowOrchestrator>();

        services.AddHostedService<
            WorkflowRecoveryHostedService>();

        services.AddPackage2Pipeline(
            configuration);

        services.AddScoped<
            Package1WorkflowDefinition>();

        services.AddScoped<
            Package1WorkflowOrchestrator>();

        services.AddScoped<
            Package1ExportService>();

        services.AddScoped<
            Package1ManifestBuilder>();

        services.AddScoped<
            Package1CleanupPipeline>();

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
            MediaClassificationWorkflowStep>();
        
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

        services.Configure<MediaSettingsOptions>(
            configuration.GetSection("MediaSettings"));
        services.Configure<HostOptions>(
            options =>
            {
                options.ShutdownTimeout =
                    TimeSpan.FromHours(3);
            });
        return services;
    }
}