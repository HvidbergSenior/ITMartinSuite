using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.FileSorter.Worker;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IWorkflowCheckpointStore _checkpointStore;

    public Worker(
        ILogger<Worker> logger,
        IWorkflowCheckpointStore checkpointStore)
    {
        _logger = logger;
        _checkpointStore = checkpointStore;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await _checkpointStore.SaveCheckpointAsync(
                Guid.NewGuid(),
                "WorkerLoop",
                "Heartbeat",
                new
                {
                    Timestamp = DateTimeOffset.UtcNow
                },
                stoppingToken);

            _logger.LogInformation("Heartbeat checkpoint saved");

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}