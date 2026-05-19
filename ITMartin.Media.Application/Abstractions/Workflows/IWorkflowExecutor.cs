namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowExecutor
{
    Task ExecuteAsync<TState>(
        IWorkflowDefinition workflow,
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class;
}