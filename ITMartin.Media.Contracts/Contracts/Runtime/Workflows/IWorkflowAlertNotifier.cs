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

    // Fired by WorkflowStallWatchdogHostedService, not WorkflowExecutor - a
    // "Running" workflow whose progress hasn't moved in a while never throws
    // (it's not Failed), so nothing else surfaces this. Without it, a hang
    // like Manifest1BuildWorkflowStep sitting stuck on a lock is silent
    // until someone thinks to ask (2026-09-07 - RicoAC sat stalled for
    // ~10 hours with zero alert before this existed).
    Task NotifyStalledAsync(
        Guid workflowId,
        string workflowName,
        string currentStep,
        TimeSpan idleFor,
        CancellationToken cancellationToken = default);

    // Fired by QuickSortAutoFinishHostedService once the FULL chain - sort,
    // then finish-library, push-to-nas, wire-gallery - has actually
    // delivered a browsable gallery, not just when the sort itself
    // completes (NotifyCompletedAsync fires for that, hours earlier). This
    // is the "it's actually done and ready to look at" signal.
    Task NotifyDeliveredAsync(
        Guid workflowId,
        string workflowName,
        string gallerySlug,
        CancellationToken cancellationToken = default);
}
