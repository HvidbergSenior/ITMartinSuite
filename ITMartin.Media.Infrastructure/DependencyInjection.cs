using ITMartin.Media.Application.Abstractions.Distributed;
using ITMartin.Media.Application.Abstractions.Nodes;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Abstractions.Queues;
using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Pipelines;
using ITMartin.Media.Application.Plugins.Abstractions;
using ITMartin.Media.Application.Processors;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Application.Services.Workflows;
using ITMartin.Media.Application.Workflow;
using ITMartin.Media.Application.Workflow.Abstractions;
using ITMartin.Media.Application.Workflow.Steps;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Infrastructure.Ai;
using ITMartin.Media.Infrastructure.Contracts.Messages;
using ITMartin.Media.Infrastructure.Distributed;
using ITMartin.Media.Infrastructure.FileSystem;
using ITMartin.Media.Infrastructure.Hashing;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Metadata;
using ITMartin.Media.Infrastructure.Notifications;
using ITMartin.Media.Infrastructure.Options;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using ITMartin.Media.Infrastructure.Plugins;
using ITMartin.Media.Infrastructure.Queues;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.Media.Infrastructure.SignalR.Runtime;
using ITMartin.Media.Infrastructure.Videos;
using ITMartin.Media.Infrastructure.Workers;
using ITMartin.Media.Infrastructure.Workflows;
using ITMartin.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("MediaDb")
            ?? "Data Source=media.db";

        services.AddDbContext<Persistence.MediaDbContext>(options =>
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

        services.AddScoped<IWorkflowCheckpointStore, EfWorkflowCheckpointStore>();

        services.AddScoped<IScanSessionStore, EfScanSessionStore>();

        // =========================
        // DISTRIBUTED
        // =========================

        services.AddSingleton<IDistributedCoordinator, InMemoryDistributedCoordinator>();

        services.AddSingleton<INodeRegistry, InMemoryNodeRegistry>();

        // =========================
        // QUEUES
        // =========================

        services.AddSingleton(typeof(IQueueTransport<>), typeof(ChannelQueueTransport<>));

        services.AddInMemoryQueue<WorkflowExecutionMessage>();

        services.AddSingleton<IMessageSerializer, SystemTextJsonMessageSerializer>();

        // =========================
        // WORKFLOWS
        // =========================

        services.AddScoped<IWorkflowEngine, WorkflowEngine>();

        services.AddScoped<IWorkflowExecutor, WorkflowExecutor>();

        services.AddScoped<IWorkflowRuntime, InMemoryWorkflowRuntime>();

        services.AddScoped<WorkflowRecoveryService>();

        services.AddScoped<FileDiscoveryWorkflowStep>();

        services.AddScoped<HashWorkflowStep>();

        services.AddScoped<MetadataWorkflowStep>();

        services.AddScoped<Package1WorkflowOrchestrator>();

        // =========================
        // RUNTIME
        // =========================

        services.AddSingleton<IRuntimeEventPublisher, SignalRRuntimeEventPublisher>();

        services.AddScoped<IWorkerHeartbeatService, WorkerHeartbeatService>();

        // =========================
        // WORKERS
        // =========================

        services.AddHostedService<ThumbnailWorker>();

        // =========================
        // PLUGINS
        // =========================


        // =========================
        // FILE SYSTEM
        // =========================

        services.AddScoped<IFileSystem, FileSystemService>();

        services.AddScoped<IFileScanner, FileScanner>();

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

        services.AddScoped<IMediaClassificationService, MediaClassificationService>();

        // =========================
        // HASHING
        // =========================

        services.AddScoped<IHashService, Sha256HashService>();

        // =========================
        // METADATA
        // =========================

        services.AddScoped<IExifService, ExifService>();

        services.AddScoped<IGpsService, GpsService>();

        services.AddScoped<IMediaDateService, MediaDateService>();

        services.AddScoped<IImageMetadataService, ImageMetadataService>();

        services.AddScoped<IVideoMetadataService, VideoMetadataService>();

        services.AddScoped<IDocumentMetadataService, DocumentMetadataService>();

        // =========================
        // VIDEO
        // =========================

        services.AddScoped<VideoConverterService>();

        services.AddScoped<IVideoBatchService, VideoBatchService>();

        services.AddScoped<SubtitleService>();

        // =========================
        // IMAGE
        // =========================

        services.AddScoped<IImageBatchService, ImageBatchService>();

        services.AddScoped<ImageConverterService>();

        services.AddScoped<ThumbnailService>();

        services.AddScoped<Package1CleanupPipeline>();

        // =========================
        // AI
        // =========================

        services.AddScoped<IAiEnrichmentService, AiEnrichmentService>();

        services.AddScoped<IImageAnalysisService, OpenAiImageAnalysisService>();

        // =========================
        // CONFIG
        // =========================

        services.Configure<MediaSettingsOptions>(
            configuration.GetSection("MediaSettings"));

        services.Configure<OpenAiOptions>(
            configuration.GetSection("OpenAI"));

        return services;
    }
}