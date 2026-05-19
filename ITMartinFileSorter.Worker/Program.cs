using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Infrastructure;
using ITMartin.Media.Infrastructure.Contracts.Messages;
using ITMartin.Media.Infrastructure.Queues;
using ITMartin.Media.Infrastructure.SignalR.Runtime;
using ITMartin.Media.Runtime.HostedServices;

var builder = Host.CreateApplicationBuilder(args);

// =========================
// MEDIA PLATFORM
// =========================

builder.Services.AddMediaInfrastructure(
    builder.Configuration);

// =========================
// RUNTIME
// =========================

builder.Services.AddSingleton<
    IRuntimeEventPublisher,
    NullRuntimeEventPublisher>();

builder.Services.AddHostedService<
    WorkflowRecoveryHostedService>();

// =========================
// QUEUES
// =========================

builder.Services.AddInMemoryQueue<
    WorkflowExecutionMessage>();
builder.Services.AddScoped<
    Package1WorkflowDefinition>();
builder.Services.AddScoped<
    FileDiscoveryWorkflowStep>();

builder.Services.AddScoped<
    HashWorkflowStep>();

builder.Services.AddScoped<
    MetadataWorkflowStep>();
// =========================
// BUILD
// =========================

var host = builder.Build();

// =========================
// RUN
// =========================

await host.RunAsync();