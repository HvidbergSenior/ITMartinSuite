using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

public interface IWorkflowCheckpointStore
{
    Task SaveCheckpointAsync<T>(
        Guid workflowId,
        string workflowName,
        string stepName,
        T state,
        CancellationToken cancellationToken = default);

    Task<T?> LoadLatestCheckpointAsync<T>(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowCheckpoint>>
        GetCheckpointHistoryAsync(
            Guid workflowId,
            CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default);
}