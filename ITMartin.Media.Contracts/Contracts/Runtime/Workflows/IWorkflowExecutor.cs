using ITMartin.Media.Runtime.Workflows;

namespace ITMartin.Media.Runtime.Interfaces;

public interface IWorkflowExecutor
{
    Task ExecuteAsync<TState>(
        IWorkflowDefinition workflow,
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class;
}