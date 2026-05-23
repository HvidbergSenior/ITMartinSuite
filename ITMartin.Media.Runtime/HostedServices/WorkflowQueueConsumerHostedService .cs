using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.Package2.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
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

    private readonly ILogger<
            WorkflowQueueConsumerHostedService>
        _logger;

    public WorkflowQueueConsumerHostedService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<WorkflowQueueConsumerHostedService> logger)
    {
        _serviceScopeFactory =
            serviceScopeFactory;

        _logger =
            logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Workflow queue consumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope =
                    _serviceScopeFactory
                        .CreateScope();

                var queue =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IBackgroundJobQueue>();

                var job =
                    await queue.DequeueAsync(
                        "workflow",
                        stoppingToken);

                if (job is null)
                {
                    await Task.Delay(
                        1000,
                        stoppingToken);

                    continue;
                }

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
                            break;
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
                            break;
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
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Workflow queue consumer error");

                await Task.Delay(
                    1000,
                    stoppingToken);
            }
        }
    }
}