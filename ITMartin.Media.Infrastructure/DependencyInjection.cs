using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Distributed;
using ITMartin.Media.Application.Abstractions.Nodes;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Abstractions.Queues;
using ITMartin.Media.Application.Pipelines.Package1;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Services;
using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Application.Processors;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Application.Services.Steps.NormalizationStep;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Domain.Steps.MetadataStep;
using ITMartin.Media.Infrastructure.Ai;
using ITMartin.Media.Infrastructure.BackgroundJobs;
using ITMartin.Media.Infrastructure.Contracts.Messages;
using ITMartin.Media.Infrastructure.Distributed;
using ITMartin.Media.Infrastructure.FileSystem;
using ITMartin.Media.Infrastructure.Hashing;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Metadata;
using ITMartin.Media.Infrastructure.Notifications;
using ITMartin.Media.Infrastructure.Options;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using ITMartin.Media.Infrastructure.Queues;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.Media.Infrastructure.Workers;
using ITMartin.Media.Runtime.Execution;
using ITMartin.Media.Runtime.HostedServices;
using ITMartin.Media.Runtime.Recovery;
using ITMartin.Media.Runtime.Registry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure;

public static class DependencyInjection
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

        // =========================
        // PERSISTENCE
        // =========================

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

        // =========================
        // DISTRIBUTED
        // =========================

        services.AddSingleton<
            IDistributedCoordinator,
            InMemoryDistributedCoordinator>();

        services.AddSingleton<
            INodeRegistry,
            InMemoryNodeRegistry>();

        // =========================
        // QUEUES
        // =========================

        services.AddSingleton(
            typeof(IQueueTransport<>),
            typeof(ChannelQueueTransport<>));

        services.AddInMemoryQueue<WorkflowExecutionMessage>();

        services.AddSingleton<
            IMessageSerializer,
            SystemTextJsonMessageSerializer>();

        services.AddSingleton<
            IBackgroundJobQueue,
            InMemoryBackgroundJobQueue>();

        // =========================
        // FILE SYSTEM
        // =========================

        services.AddScoped<
            IFileSystem,
            FileSystemService>();

        services.AddScoped<
            IFileScanner,
            FileScanner>();

        // =========================
        // PROCESSORS
        // =========================

        services.AddScoped<MediaSupportProcessor>();

        services.AddScoped<FileCreationProcessor>();

        services.AddScoped<MetadataProcessor>();

        services.AddScoped<HashProcessor>();

        services.AddScoped<ClassificationProcessor>();

        services.AddScoped<DuplicateProcessor>();

        services.AddScoped<ParallelScanProcessor>();

        services.AddScoped<FileEnumerationProcessor>();

        services.AddScoped<ExportStatisticsProcessor>();

        services.AddScoped<KeepFileProcessor>();

        services.AddScoped<DeleteFileProcessor>();

        services.AddScoped<FileSizeProcessor>();

        services.AddScoped<DuplicateGroupProcessor>();

        services.AddScoped<MediaCategoryProcessor>();

        services.AddScoped<ReviewProcessor>();

        services.AddScoped<OcrSupportProcessor>();

        services.AddScoped<MediaTypeProcessor>();

        services.AddScoped<NormalizationProcessor>();

        services.AddScoped<ExportPathProcessor>();

        // =========================
        // CLASSIFICATION
        // =========================

        services.AddScoped<
            IMediaClassificationService,
            MediaClassificationService>();

        // =========================
        // HASHING
        // =========================

        services.AddScoped<
            IHashService,
            Sha256HashService>();

        // =========================
        // METADATA
        // =========================

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

        // =========================
        // VIDEO
        // =========================

        services.AddScoped<VideoConverterService>();

        services.AddScoped<
            IVideoBatchService,
            VideoBatchService>();

        services.AddScoped<SubtitleService>();

        // =========================
        // IMAGE
        // =========================

        services.AddScoped<
            IImageBatchService,
            ImageBatchService>();

        services.AddScoped<ImageConverterService>();

        services.AddScoped<ThumbnailService>();

        // =========================
        // AI
        // =========================

        services.AddScoped<
            IAiEnrichmentService,
            AiEnrichmentService>();

        services.AddScoped<
            IImageAnalysisService,
            OpenAiImageAnalysisService>();

        // =========================
        // CONFIG
        // =========================

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
        // =========================
        // WORKFLOW RUNTIME
        // =========================

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

        // =========================
        // HOSTED SERVICES
        // =========================

        services.AddHostedService<
            WorkflowRecoveryHostedService>();

        services.AddHostedService<
            ThumbnailWorker>();

        // =========================
        // WORKFLOWS
        // =========================

        services.AddScoped<
            Package1WorkflowDefinition>();

        services.AddScoped<
            IWorkflowDefinition,
            Package1WorkflowDefinition>();

        services.AddScoped<
            Package1WorkflowOrchestrator>();

        services.AddScoped<
            Package1ExportService>();

        services.AddScoped<
            Package1ManifestBuilder>();

        services.AddScoped<
            Package1CleanupPipeline>();

        // =========================
        // STEPS
        // =========================

        services.AddScoped<
            FileDiscoveryWorkflowStep>();

        services.AddScoped<
            HashWorkflowStep>();

        services.AddScoped<
            MetadataWorkflowStep>();

        services.AddScoped<
            ImageNormalizationWorkflowStep>();

        services.AddScoped<
            VideoNormalizationWorkflowStep>();

        services.AddScoped<
            ThumbnailWorkflowStep>();

        services.AddScoped<
            DuplicateDetectionWorkflowStep>();

        services.AddScoped<
            CleanupEvaluationWorkflowStep>();

        services.AddScoped<
            ManifestBuildWorkflowStep>();

        services.AddScoped<
            ExportWorkflowExecutionStep>();

        // =========================
        // CONFIG
        // =========================

        services.Configure<MediaSettingsOptions>(
            configuration.GetSection("MediaSettings"));

        return services;
    }
}