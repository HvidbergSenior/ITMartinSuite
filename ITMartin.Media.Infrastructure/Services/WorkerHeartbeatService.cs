// File: ITMartin.Media.Infrastructure/Runtime/WorkerHeartbeatService.cs

using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Infrastructure.Contracts.Messages;

namespace ITMartin.Media.Infrastructure.Services;

public sealed class WorkerHeartbeatService(
    IRuntimeEventPublisher runtimeEventPublisher)
    : IWorkerHeartbeatService
{
    private readonly string _nodeId = Environment.MachineName;

    public async Task PublishHeartbeatAsync(
        string workerName,
        string status,
        int activeWorkflows,
        CancellationToken cancellationToken = default)
    {
        var message = new WorkerHeartbeatMessage(
            _nodeId,
            workerName,
            status,
            activeWorkflows,
            DateTimeOffset.UtcNow);

        await runtimeEventPublisher.PublishAsync(
            message,
            cancellationToken);
    }
}