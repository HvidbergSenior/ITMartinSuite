using ITMartin.Media.Runtime.Workflows;

namespace ITMartin.Media.Runtime.Interfaces;

public interface IWorkflowStep
{
    string Name { get; }

    Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class;
}