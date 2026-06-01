using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class SaveMagicCardWorkflowStep
    : WorkflowStep<CardScanContext>
{
    public override string Name =>
        nameof(SaveMagicCardWorkflowStep);

    public override Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}