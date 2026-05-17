namespace ITMartin.Media.Infrastructure.Workers;

using ITMartin.Media.Application.Abstractions.Workers;
using ITMartin.Media.Application.Models.Workers;

public sealed class InMemoryWorkerHeartbeatService
    : IWorkerHeartbeatService
{
    private static readonly List<WorkerHeartbeat>
        Heartbeats = [];

    public Task ReportAsync(
        WorkerHeartbeat heartbeat,
        CancellationToken cancellationToken)
    {
        Heartbeats.RemoveAll(x =>
            x.WorkerName == heartbeat.WorkerName);

        Heartbeats.Add(heartbeat);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<WorkerHeartbeat>>
        GetAllAsync(
            CancellationToken cancellationToken)
    {
        IReadOnlyCollection<WorkerHeartbeat>
            results =
                Heartbeats.ToList();

        return Task.FromResult(results);
    }
}