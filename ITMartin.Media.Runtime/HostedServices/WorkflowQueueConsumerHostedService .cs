using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
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

                await orchestrator.ResumeAsync(
                    Guid.Parse(job.Payload),
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