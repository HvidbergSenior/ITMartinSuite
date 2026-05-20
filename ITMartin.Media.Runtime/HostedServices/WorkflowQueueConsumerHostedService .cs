using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
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
                    _serviceScopeFactory.CreateScope();

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

                var orchestrator =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IScanOrchestrator>();
                
                _logger.LogInformation(
                    "Dequeued workflow job");

                var request =
                    System.Text.Json.JsonSerializer
                        .Deserialize<StartScanRequest>(
                            job.Payload);

                if (request is null)
                {
                    continue;
                }

                await orchestrator.StartAsync(
                    request,
                    stoppingToken);
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