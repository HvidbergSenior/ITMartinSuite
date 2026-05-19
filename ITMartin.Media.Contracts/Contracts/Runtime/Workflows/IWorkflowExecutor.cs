using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IWorkflowExecutor
{
    Task ExecuteAsync<TState>(
        IWorkflowDefinition workflow,
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class;
}