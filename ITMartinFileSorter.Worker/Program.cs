using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Application.Services.Steps.DuplicationStep;
using ITMartin.Media.Application.Services.Steps.ExportStep;
using ITMartin.Media.Application.Services.Steps.NormalizationStep;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure;
using ITMartin.Media.Infrastructure.BackgroundJobs;
using ITMartin.Media.Infrastructure.Events;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Media;
using ITMartin.Media.Infrastructure.Persistence.Repositories;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.Media.Infrastructure.SignalR.Runtime;
using ITMartin.Media.Runtime.HostedServices;

var builder = Host.CreateApplicationBuilder(args);

// =========================
// MEDIA PLATFORM
// =========================

builder.Services.AddMediaInfrastructureCore(
    builder.Configuration);
builder.Services.AddMediaWorkflowRuntime(
    builder.Configuration);
// =========================
// CORE SERVICES
// =========================

builder.Services.AddScoped<
    IMediaTypeResolver,
    MediaTypeResolver>();

builder.Services.AddScoped<
    IThumbnailService,
    ThumbnailService>();

builder.Services.AddScoped<
    IDuplicateService,
    DuplicateService>();
// =========================
// NORMALIZATION
// =========================
builder.Services.AddHostedService<
    WorkflowQueueConsumerHostedService>();
builder.Services.AddScoped<
    IImageConverterService,
    ImageConverterService>();
builder.Services.AddScoped<
    IThumbnailService,
    ThumbnailService>();
builder.Services.AddScoped<
    IDuplicateService,
    DuplicateService>();
// RUNTIME
// =========================
builder.Services.AddScoped<
    IMediaNamingService,
    MediaNamingService>();
builder.Services.AddScoped<
    IScanOrchestrator,
    Package1WorkflowOrchestrator>();
builder.Services.AddScoped<
    IScanSessionRepository,
    ScanSessionRepository>();
builder.Services.AddScoped<
    Package1WorkflowDefinition>();
builder.Services.AddSingleton<
    IEventPublisher,
    NullEventPublisher>();
builder.Services.AddSingleton<
    IRuntimeEventPublisher,
    NullRuntimeEventPublisher>();
builder.Services.AddScoped<
    ILibraryExportService,
    LibraryExportService>();
builder.Services.AddSingleton<
    IBackgroundJobQueue,
    RabbitMqBackgroundJobQueue>();
var libraryRoot =
    builder.Configuration[
        "MediaSettings:LibraryRoot"];

Console.WriteLine(
    $"LIBRARY ROOT: {libraryRoot}");

builder.Services.AddScoped<
    ILibraryPathProvider,
    LibraryPathProvider>();
// =========================
// QUEUES
// =========================
builder.Logging.ClearProviders();

builder.Logging.AddConsole();

builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore",
    LogLevel.None);

builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore.Database.Command",
    LogLevel.None);

// =========================
// BUILD
// =========================

var host = builder.Build();

// =========================
// RUN
// =========================

await host.RunAsync();