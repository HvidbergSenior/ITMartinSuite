// File: ITMartin.FileSorter.Worker/Program.cs

using ITMartin.FileSorter.Worker;
using ITMartin.Media.Infrastructure;
using ITMartin.Media.Infrastructure.Contracts.Messages;
using ITMartin.Media.Infrastructure.Queues;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// =========================
// MEDIA PLATFORM
// =========================

builder.Services.AddMediaInfrastructure(
    builder.Configuration);

// =========================
// QUEUES
// =========================

builder.Services.AddInMemoryQueue<
    WorkflowExecutionMessage>();

// =========================
// WORKERS
// =========================

builder.Services.AddHostedService<Worker>();

// =========================
// BUILD
// =========================

var host = builder.Build();

// =========================
// RUN
// =========================

await host.RunAsync();