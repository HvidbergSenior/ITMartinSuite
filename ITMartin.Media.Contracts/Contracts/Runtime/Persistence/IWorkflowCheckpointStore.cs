using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Persistence;


public interface IWorkflowCheckpointStore
{
    Task SaveCheckpointAsync(
        Guid workflowId,
        string workflowName,
        string stepName,
        object state,
        CancellationToken cancellationToken = default);

    Task<T?> LoadCheckpointAsync<T>(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default);

    Task<WorkflowCheckpoint?> GetLatestCheckpointAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default);
}