using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

namespace ITMartin.Magic.Infrastructure.Workflows;

public sealed class NullWorkflowCheckpointStore
    : IWorkflowCheckpointStore
{
    public Task SaveCheckpointAsync<T>(
        Guid workflowId,
        string workflowName,
        string stepName,
        T state,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<T?> LoadLatestCheckpointAsync<T>(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<T?>(default);
    }

    public Task<IReadOnlyList<WorkflowCheckpoint>>
        GetCheckpointHistoryAsync(
            Guid workflowId,
            CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<WorkflowCheckpoint>>([]);
    }

    public Task MarkCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}