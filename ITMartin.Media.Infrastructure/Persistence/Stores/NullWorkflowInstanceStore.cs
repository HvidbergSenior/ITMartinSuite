using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

namespace ITMartin.Magic.Infrastructure.Workflows;

public sealed class NullWorkflowInstanceStore
    : IWorkflowInstanceStore
{
    public Task CreateAsync(
        Guid workflowId,
        string workflowName,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SetRunningStepAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task MarkCompletedAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(
        Guid workflowId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<IReadOnlyCollection<Guid>>
        GetRecoverableWorkflowIdsAsync(
            CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }

    public Task SetProgressAsync(
        Guid workflowId,
        int current,
        int total,
        string? item = null,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}