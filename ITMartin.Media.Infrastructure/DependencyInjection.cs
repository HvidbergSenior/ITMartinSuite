using ITMartin.Media.Application.Pipelines;
using ITMartin.Media.Application.Processors;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Infrastructure.Ai;
using ITMartin.Media.Infrastructure.FileSystem;
using ITMartin.Media.Infrastructure.Hashing;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Metadata;
using ITMartin.Media.Infrastructure.Options;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.Media.Infrastructure.Videos;
using ITMartin.Media.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection
        AddMediaInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
    {
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

        services.AddScoped<
            MediaSupportProcessor>();

        services.AddScoped<
            FileCreationProcessor>();

        services.AddScoped<
            MetadataProcessor>();

        services.AddScoped<
            HashProcessor>();

        services.AddScoped<
            ClassificationProcessor>();

        services.AddScoped<
            DuplicateProcessor>();
        services.AddScoped<
            ParallelScanProcessor>();
        services.AddScoped<
            FileEnumerationProcessor>();
        services.AddScoped<
            ExportStatisticsProcessor>();

        services.AddScoped<
            KeepFileProcessor>();

        services.AddScoped<
            DeleteFileProcessor>();

        services.AddScoped<
            FileSizeProcessor>();

        services.AddScoped<
            DuplicateGroupProcessor>();

        services.AddScoped<
            MediaCategoryProcessor>();

        services.AddScoped<
            ReviewProcessor>();

        services.AddScoped<
            OcrSupportProcessor>();

        services.AddScoped<
            MediaTypeProcessor>();
        services.AddScoped<
            NormalizationProcessor>();

        services.AddScoped<
            ExportPathProcessor>();
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

        services.AddScoped<
            VideoConverterService>();

        services.AddScoped<
            IVideoBatchService,
            VideoBatchService>();

        services.AddScoped<
            SubtitleService>();

        // =========================
        // IMAGE
        // =========================

        services.AddScoped<
            IImageBatchService,
            ImageBatchService>();

        services.AddScoped<
            ImageConverterService>();

        services.AddScoped<
            ThumbnailService>();
        services.AddScoped<
            Package1CleanupPipeline>();
        // =========================
        // AI
        // KEEP FOR PACKAGE 2
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

        services.Configure<
            MediaSettingsOptions>(
            configuration.GetSection(
                "MediaSettings"));

        services.Configure<
            OpenAiOptions>(
            configuration.GetSection(
                "OpenAI"));

        return services;
    }
}