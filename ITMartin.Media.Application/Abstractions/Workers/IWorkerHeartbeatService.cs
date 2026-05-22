using ITMartin.Media.Application.Pipelines.Package1.Models.Workers;

namespace ITMartin.Media.Application.Abstractions.Workers;

public interface IWorkerHeartbeatService
{
    Task ReportAsync(
        WorkerHeartbeat heartbeat,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkerHeartbeat>>
        GetAllAsync(
            CancellationToken cancellationToken);
}