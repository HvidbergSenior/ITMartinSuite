using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

namespace ITMartin.Magic.Infrastructure.Workflows;

public sealed class NullWorkflowStepExecutionStore
    : IWorkflowStepExecutionStore
{
    public Task<bool> IsCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task MarkStartedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task MarkCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}