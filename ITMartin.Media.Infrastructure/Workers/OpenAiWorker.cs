namespace ITMartin.Media.Infrastructure.Workers;

using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Workers;
using ITMartin.Media.Application.Models.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class OpenAiWorker
    : BackgroundService, IWorker
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<OpenAiWorker>
        _logger;

    private readonly IWorkerHeartbeatService
        _heartbeatService;

    public OpenAiWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OpenAiWorker> logger,
        IWorkerHeartbeatService heartbeatService)
    {
        _scopeFactory =
            scopeFactory;

        _logger =
            logger;

        _heartbeatService =
            heartbeatService;
    }

    public async Task ExecuteJobAsync(
        CancellationToken cancellationToken)
    {
        using var scope =
            _scopeFactory
                .CreateScope();

        var queue =
            scope.ServiceProvider
                .GetRequiredService<
                    IBackgroundJobQueue>();

        var job =
            await queue.DequeueAsync(
                "openai",
                cancellationToken);

        if (job is null)
        {
            return;
        }

        _logger.LogInformation(
            "Processing OpenAI job {JobId}",
            job.Id);

        await Task.Delay(
            500,
            cancellationToken);

        _logger.LogInformation(
            "Completed OpenAI job {JobId}",
            job.Id);
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _heartbeatService.ReportAsync(
                new WorkerHeartbeat
                {
                    WorkerName =
                        nameof(OpenAiWorker),

                    LastSeenAt =
                        DateTimeOffset.UtcNow,

                    ActiveJobs = 1,

                    IsHealthy = true
                },
                stoppingToken);

            await ExecuteJobAsync(
                stoppingToken);

            await Task.Delay(
                1000,
                stoppingToken);
        }
    }
}