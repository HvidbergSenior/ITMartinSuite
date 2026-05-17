namespace ITMartin.Media.Application.Abstractions.Workflows;

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

    Task MarkCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default);
}