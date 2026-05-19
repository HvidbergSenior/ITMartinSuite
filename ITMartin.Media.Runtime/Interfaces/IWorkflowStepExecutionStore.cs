namespace ITMartin.Media.Runtime.Interfaces;

public interface IWorkflowStepExecutionStore
{
    Task<bool> IsCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default);

    Task MarkStartedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default);
}