namespace ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

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

    // Puts an existing instance back into "Running" - needed when recovery
    // re-executes a workflow that was left Running or Failed. Without it the
    // row keeps its stale status for the whole re-run, so every status
    // consumer (the p1-status endpoint, the stall watchdog, auto-finish)
    // reports a live workflow as failed.
    Task MarkRunningAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> GetRecoverableWorkflowIdsAsync(
        CancellationToken cancellationToken = default);

    Task SetProgressAsync(
        Guid workflowId,
        int current,
        int total,
        string? item = null,
        IReadOnlyDictionary<string, int>? counts = null,
        CancellationToken cancellationToken = default);
}