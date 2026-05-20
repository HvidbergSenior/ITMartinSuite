namespace ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

using ITMartin.Media.Contracts.Contracts.Runtime.Models;

public interface IWorkflowExecutionStateStore
{
    Task SaveAsync<TState>(
        Guid workflowId,
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class;

    Task<WorkflowExecutionContext<TState>?> LoadAsync<TState>(
        Guid workflowId,
        CancellationToken cancellationToken = default)
        where TState : class;
}