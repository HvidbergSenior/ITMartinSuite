// File: ITMartinFileSorter.Server/Program.cs

using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Application.Services.Steps.DuplicationStep;
using ITMartin.Media.Application.Services.Steps.ExportStep;
using ITMartin.Media.Application.Services.Steps.NormalizationStep;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure;
using ITMartin.Media.Infrastructure.Contracts.Messages;
using ITMartin.Media.Infrastructure.Events;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Media;
using ITMartin.Media.Infrastructure.Persistence.Repositories;
using ITMartin.Media.Infrastructure.Queues;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.Media.Infrastructure.SignalR.Runtime;
using ITMartin.Media.Runtime.HostedServices;
using ITMartin.Media.Runtime.Recovery;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;
using ITMartinFileSorter.Server;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// =========================
// BLAZOR
// =========================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = true;
    });

// =========================
// SIGNALR
// =========================

builder.Services.AddMediaSignalR();

// =========================
// MEDIA PLATFORM
// =========================

builder.Services.AddMediaInfrastructure(
    builder.Configuration);

// =========================
// CORE SERVICES
// =========================

builder.Services.AddScoped<
    IMediaTypeResolver,
    MediaTypeResolver>();

builder.Services.AddScoped<
    IImageConverterService,
    ImageConverterService>();

builder.Services.AddScoped<
    IThumbnailService,
    ThumbnailService>();

builder.Services.AddScoped<
    IDuplicateService,
    DuplicateService>();

builder.Services.AddScoped<
    IMediaNamingService,
    MediaNamingService>();

builder.Services.AddScoped<
    ILibraryExportService,
    LibraryExportService>();

builder.Services.AddScoped<
    IScanSessionRepository,
    ScanSessionRepository>();

builder.Services.AddScoped<
    IWorkflowRecoveryService,
    WorkflowRecoveryService>();
builder.Logging.ClearProviders();

builder.Logging.AddConsole();

builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore",
    LogLevel.None);

builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore.Database.Command",
    LogLevel.None);
// =========================
// EVENTS
// =========================

builder.Services.AddSingleton<
    IEventPublisher,
    NullEventPublisher>();

builder.Services.AddSingleton<
    IRuntimeEventPublisher,
    NullRuntimeEventPublisher>();

builder.Services.AddScoped<
    ILibraryPathProvider,
    LibraryPathProvider>();
// =========================
// AI
// =========================

builder.Services.AddScoped<
    IMediaVisionService,
    MediaVisionService>();

builder.Services.AddScoped<
    IAiCollectionService,
    AiCollectionService>();

builder.Services.AddScoped<
    IAiCacheService,
    SqliteAiCacheService>();

builder.Services.AddScoped<
    IMediaOcrService,
    MediaOcrService>();

// =========================
// OCR
// =========================

builder.Services.AddSingleton<
    IOcrService,
    OcrService>();

// =========================
// UI
// =========================

builder.Services.AddScoped<
    ProgressService>();

builder.Services.AddScoped(sp =>
{
    var navigation =
        sp.GetRequiredService<
            NavigationManager>();

    return new HttpClient
    {
        BaseAddress =
            new Uri(
                navigation.BaseUri)
    };
});

// =========================
// QUEUES
// =========================

builder.Services.AddInMemoryQueue<
    WorkflowExecutionMessage>();

// =========================
// HOSTED SERVICES
// =========================

builder.Services.AddHostedService<
    WorkflowRecoveryHostedService>();

// =========================
// WORKFLOW
// =========================

builder.Services.AddScoped<
    Package1WorkflowDefinition>();

builder.Services.AddScoped<
    Package1WorkflowOrchestrator>();

builder.Services.AddScoped<
    Package1ExportService>();

// =========================
// WORKFLOW STEPS
// =========================

builder.Services.AddScoped<
    FileDiscoveryWorkflowStep>();

builder.Services.AddScoped<
    HashWorkflowStep>();

builder.Services.AddScoped<
    MetadataWorkflowStep>();

builder.Services.AddScoped<
    ImageNormalizationWorkflowStep>();

builder.Services.AddScoped<
    VideoNormalizationWorkflowStep>();

builder.Services.AddScoped<
    ThumbnailWorkflowStep>();

builder.Services.AddScoped<
    DuplicateDetectionWorkflowStep>();

builder.Services.AddScoped<
    CleanupEvaluationWorkflowStep>();

builder.Services.AddScoped<
    ManifestBuildWorkflowStep>();

builder.Services.AddScoped<
    ExportWorkflowExecutionStep>();

// =========================
// CONTROLLERS
// =========================

builder.Services.AddControllers();

// =========================
// BUILD
// =========================

var app = builder.Build();

// =========================
// ERROR HANDLING
// =========================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error");
}

// =========================
// STATIC FILES
// =========================

app.UseStaticFiles();

// =========================
// LIBRARY FILES
// =========================

var libraryPath =
    builder.Configuration[
        "MediaSettings:LibraryRoot"];

var provider =
    new FileExtensionContentTypeProvider();

provider.Mappings[".mp4"] =
    "video/mp4";

provider.Mappings[".mov"] =
    "video/quicktime";

provider.Mappings[".mkv"] =
    "video/x-matroska";

provider.Mappings[".jpg"] =
    "image/jpeg";

provider.Mappings[".jpeg"] =
    "image/jpeg";

provider.Mappings[".png"] =
    "image/png";

provider.Mappings[".webp"] =
    "image/webp";

provider.Mappings[".gif"] =
    "image/gif";

provider.Mappings[".heic"] =
    "image/heic";

provider.Mappings[".avif"] =
    "image/avif";

if (!string.IsNullOrWhiteSpace(
        libraryPath) &&
    Directory.Exists(
        libraryPath))
{
    app.UseStaticFiles(
        new StaticFileOptions
        {
            FileProvider =
                new PhysicalFileProvider(
                    libraryPath),

            RequestPath =
                "/libraryfiles",

            ContentTypeProvider =
                provider
        });
}

// =========================
// PIPELINE
// =========================

app.UseAntiforgery();

app.MapControllers();

app.MapMediaSignalRHubs();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// =========================
// RUN
// =========================

app.Run();