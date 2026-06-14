using ITMartin.Media.Application.Abstractions.BackgroundJobs;
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
            try
            {
                using var scope =
                    _serviceScopeFactory.CreateScope();

                _logger.LogInformation(
                    "Dequeued workflow job {Type}",
                    job.Type);
                
                var handler =
                    scope.ServiceProvider
                        .GetServices<IBackgroundJobHandler>()
                        .FirstOrDefault(
                            x => x.JobType == job.Type);

                if (handler is null)
                {
                    _logger.LogWarning(
                        "No handler found for job type {Type}",
                        job.Type);

                    return;
                }

                await handler.HandleAsync(
                    job,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed processing workflow job {Type}",
                    job.Type);
            }
        });

    return Task.CompletedTask;
}
}