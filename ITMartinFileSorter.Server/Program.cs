
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1;
using ITMartin.Media.Application.Pipelines.Package1.Clients;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Services;
using ITMartin.Media.Application.Pipelines.Package2.Clients;
using ITMartin.Media.Application.Pipelines.Package2.Orchestration;
using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Application.Services.Steps.DuplicationStep;
using ITMartin.Media.Application.Services.Steps.ExportStep;
using ITMartin.Media.Application.Services.Steps.NormalizationStep;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure;
using ITMartin.Media.Infrastructure.BackgroundJobs;
using ITMartin.Media.Infrastructure.Contracts.Messages;
using ITMartin.Media.Infrastructure.Events;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Media;
using ITMartin.Media.Infrastructure.Persistence.Repositories;
using ITMartin.Media.Infrastructure.Queues;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.Media.Infrastructure.SignalR.Runtime;
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

builder.Services.AddMediaInfrastructureCore(
    builder.Configuration);

// =========================
// OCR
// =========================

builder.Services.AddSingleton<
    IOcrService,
    OcrService>();

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
    Package1ManifestWriter>();
builder.Services.AddScoped<
    Package1ManifestSummaryService>();
builder.Services.AddScoped<
    Package1ManifestLoader>();
builder.Services.AddScoped<
    IPackage2Client,
    Package2Client>();
builder.Services.AddScoped<
    IPackage1Client,
    Package1Client>();
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
// CONTROLLERS
// =========================

builder.Services.AddControllers();

builder.Services.AddSingleton<
    IBackgroundJobQueue,
    RabbitMqBackgroundJobQueue>();
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