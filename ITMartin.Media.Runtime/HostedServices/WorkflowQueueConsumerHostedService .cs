using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.Package2.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.HostedServices;

public sealed class WorkflowQueueConsumerHostedService
    : BackgroundService
{
    private readonly IServiceScopeFactory
        _serviceScopeFactory;

    private readonly IBackgroundJobQueue
        _queue;

    private readonly ILogger<
            WorkflowQueueConsumerHostedService>
        _logger;

    public WorkflowQueueConsumerHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IBackgroundJobQueue queue,
        ILogger<WorkflowQueueConsumerHostedService> logger)
    {
        _serviceScopeFactory =
            serviceScopeFactory;

        _queue =
            queue;

        _logger =
            logger;
    }

    protected override Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Workflow queue consumer started");

        _queue.Subscribe(
            "workflow",
            async job =>
            {
                using var scope =
                    _serviceScopeFactory
                        .CreateScope();

                _logger.LogInformation(
                    "Dequeued workflow job {Type}",
                    job.Type);

                switch (job.Type)
                {
                    case "StartPackage1":
                    {
                        var orchestrator =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    IScanOrchestrator>();

                        var request =
                            JsonSerializer
                                .Deserialize<
                                    Package1WorkflowState>(
                                        job.Payload);

                        if (request is null)
                        {
                            return;
                        }

                        await orchestrator
                            .StartAsync(
                                request,
                                stoppingToken);

                        break;
                    }

                    case "StartPackage2":
                    {
                        var orchestrator =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    Package2WorkflowOrchestrator>();

                        var request =
                            JsonSerializer
                                .Deserialize<
                                    StartPackage2Request>(
                                        job.Payload);

                        if (request is null)
                        {
                            return;
                        }

                        await orchestrator
                            .RunAsync(
                                request,
                                stoppingToken);

                        break;
                    }

                    default:
                    {
                        _logger.LogWarning(
                            "Unknown workflow job type {Type}",
                            job.Type);

                        break;
                    }
                }
            });

        return Task.CompletedTask;
    }
}