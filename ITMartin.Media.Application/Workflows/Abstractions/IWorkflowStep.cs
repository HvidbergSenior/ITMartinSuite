using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.Media.Application.Workflows.Abstractions;

public interface IWorkflowStep
{
    string Name { get; }

    Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class;
}