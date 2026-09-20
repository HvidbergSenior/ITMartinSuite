using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Receipt.Infrastructure.Workflows;

// Receipt runs its workflows inline in the UI; there is nobody to alert.
public sealed class NoopWorkflowAlertNotifier : IWorkflowAlertNotifier
{
    public Task NotifyFailedAsync(Guid workflowId, string workflowName, string errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyCompletedAsync(Guid workflowId, string workflowName, TimeSpan duration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyStalledAsync(Guid workflowId, string workflowName, string currentStep, TimeSpan idleFor, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyDeliveredAsync(Guid workflowId, string workflowName, string gallerySlug, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
