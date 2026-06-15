using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

namespace ITMartin.Receipt.Infrastructure.Workflows;

internal sealed class NullWorkflowCheckpointStore : IWorkflowCheckpointStore
{
    public Task SaveCheckpointAsync<T>(Guid workflowId, string workflowName, string stepName, T state, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<T?> LoadLatestCheckpointAsync<T>(Guid workflowId, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
    public Task<IReadOnlyList<WorkflowCheckpoint>> GetCheckpointHistoryAsync(Guid workflowId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkflowCheckpoint>>([]);
    public Task MarkCompletedAsync(Guid workflowId, string stepName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class NullWorkflowStepExecutionStore : IWorkflowStepExecutionStore
{
    public Task MarkStartedAsync(Guid workflowId, string stepName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task MarkCompletedAsync(Guid workflowId, string stepName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> IsCompletedAsync(Guid workflowId, string stepName, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

internal sealed class NullWorkflowInstanceStore : IWorkflowInstanceStore
{
    public Task CreateAsync(Guid workflowId, string workflowName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetRunningStepAsync(Guid workflowId, string stepName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task MarkCompletedAsync(Guid workflowId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task MarkFailedAsync(Guid workflowId, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> ExistsAsync(Guid workflowId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<IReadOnlyCollection<Guid>> GetRecoverableWorkflowIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
}
