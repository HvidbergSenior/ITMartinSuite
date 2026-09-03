namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

/// <summary>
/// Fired by WorkflowExecutor on failure/completion of any workflow run
/// across the suite. Default registration is a no-op (see
/// NullWorkflowAlertNotifier) - only apps that want out-of-band alerting
/// (FileSorter) override it with a real implementation.
/// </summary>
public interface IWorkflowAlertNotifier
{
    Task NotifyFailedAsync(
        Guid workflowId,
        string workflowName,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task NotifyCompletedAsync(
        Guid workflowId,
        string workflowName,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
