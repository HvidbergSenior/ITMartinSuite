using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Application.Services.Steps.DuplicationStep;
using ITMartin.Media.Application.Services.Steps.NormalizationStep;
using ITMartin.Media.Contracts.Contracts.Runtime.Services;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Steps.NormalizationStep;
using ITMartin.Media.Infrastructure;
using ITMartin.Media.Infrastructure.Contracts.Messages;
using ITMartin.Media.Infrastructure.Events;
using ITMartin.Media.Infrastructure.Images;
using ITMartin.Media.Infrastructure.Media;
using ITMartin.Media.Infrastructure.Persistence.Repositories;
using ITMartin.Media.Infrastructure.Queues;
using ITMartin.Media.Infrastructure.SignalR.Runtime;
using ITMartin.Media.Runtime.HostedServices;
using ITMartin.Media.Runtime.Recovery;

var builder = Host.CreateApplicationBuilder(args);

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
    IWorkflowRecoveryService,
    WorkflowRecoveryService>();

// =========================
// NORMALIZATION
// =========================

builder.Services.AddScoped<
IWorkflowRecoveryService,
WorkflowRecoveryService > ();
// RUNTIME
// =========================
builder.Services.AddScoped<
    Package1ExportService>();
builder.Services.AddScoped<
    IMediaNamingService,
    MediaNamingService>();
builder.Services.AddScoped<
    IScanSessionRepository,
    ScanSessionRepository>();
builder.Services.AddSingleton<
    IEventPublisher,
    NullEventPublisher>();
builder.Services.AddSingleton<
    IRuntimeEventPublisher,
    NullRuntimeEventPublisher>();
builder.Services.AddScoped<
    ILibraryExportService,
    LibraryExportService>();
builder.Services.AddHostedService<
    WorkflowRecoveryHostedService>();

// =========================
// QUEUES
// =========================

builder.Services.AddInMemoryQueue<
    WorkflowExecutionMessage>();

// =========================
// WORKFLOW
// =========================

builder.Services.AddScoped<
    Package1WorkflowDefinition>();

builder.Services.AddScoped<
    Package1WorkflowOrchestrator>();

// =========================
// STEPS
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
// BUILD
// =========================

var host = builder.Build();

// =========================
// RUN
// =========================

await host.RunAsync();