using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public abstract class WorkflowStep<TState>
    : IWorkflowStep
    where TState : class
{
    public abstract string Name { get; }

    public abstract Task ExecuteAsync(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default);

    public async Task ExecuteAsync<TWorkflowState>(
        WorkflowExecutionContext<TWorkflowState> context,
        CancellationToken cancellationToken = default)
        where TWorkflowState : class
    {
        if (context.State is not TState typedState)
        {
            throw new InvalidOperationException(
                $"Invalid workflow state type. Expected {typeof(TState).Name}.");
        }

        await ExecuteAsync(
            new WorkflowExecutionContext<TState>
            {
                State = typedState,
                WorkflowName = context.WorkflowName
            },
            cancellationToken);
    }
}