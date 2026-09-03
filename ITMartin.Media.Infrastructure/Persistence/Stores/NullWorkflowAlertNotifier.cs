using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

public sealed class NullWorkflowAlertNotifier : IWorkflowAlertNotifier
{
    public Task NotifyFailedAsync(
        Guid workflowId,
        string workflowName,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task NotifyCompletedAsync(
        Guid workflowId,
        string workflowName,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
