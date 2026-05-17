namespace ITMartin.Media.Application.Abstractions.Workers;

using ITMartin.Media.Application.Models.Workers;

public interface IWorkerHeartbeatService
{
    Task ReportAsync(
        WorkerHeartbeat heartbeat,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkerHeartbeat>>
        GetAllAsync(
            CancellationToken cancellationToken);
}