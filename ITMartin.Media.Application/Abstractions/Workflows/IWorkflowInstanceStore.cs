namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowInstanceStore
{
    Task CreateAsync(
        Guid workflowId,
        string workflowName,
        CancellationToken cancellationToken = default);

    Task SetRunningStepAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid workflowId,
        string reason,
        CancellationToken cancellationToken = default);
}