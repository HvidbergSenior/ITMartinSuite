// File: ITMartin.Media.Application/Abstractions/Runtime/IWorkerHeartbeatService.cs

namespace ITMartin.Media.Application.Abstractions.Runtime;

public interface IWorkerHeartbeatService
{
    Task PublishHeartbeatAsync(
        string workerName,
        string status,
        int activeWorkflows,
        CancellationToken cancellationToken = default);
}