using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public abstract class CardScanWorkflowStepBase
    : IWorkflowStep
{
    public abstract string Name { get; }

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var state =
            context.State as CardScanWorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state.");

        await ExecuteInternalAsync(
            state,
            cancellationToken);
    }

    protected abstract Task ExecuteInternalAsync(
        CardScanWorkflowState state,
        CancellationToken cancellationToken);
}