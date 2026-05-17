using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.FileSorter.Worker;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var checkpointStore =
                    scope.ServiceProvider
                        .GetRequiredService<IWorkflowCheckpointStore>();

                await checkpointStore.SaveCheckpointAsync(
                    Guid.NewGuid(),
                    "WorkerLoop",
                    "Heartbeat",
                    new
                    {
                        Timestamp = DateTimeOffset.UtcNow
                    },
                    stoppingToken);

                _logger.LogInformation("Heartbeat checkpoint saved");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to save heartbeat checkpoint");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(10),
                stoppingToken);
        }
    }
}