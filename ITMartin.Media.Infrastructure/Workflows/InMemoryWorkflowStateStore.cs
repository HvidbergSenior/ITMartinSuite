using System.Collections.Concurrent;
using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.Media.Infrastructure.Workflows;

public sealed class InMemoryWorkflowStateStore
    : IWorkflowStateStore
{
    private static readonly ConcurrentDictionary<Guid, string>
        Checkpoints = new();

    public Task<string?> GetLastCompletedStepAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        Checkpoints.TryGetValue(
            workflowId,
            out var value);

        return Task.FromResult(value);
    }

    public Task SaveCheckpointAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        Checkpoints[workflowId] =
            stepName;

        return Task.CompletedTask;
    }
}