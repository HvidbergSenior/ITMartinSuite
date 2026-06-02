using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class ClassifyFrameWorkflowStep
    : WorkflowStep<CardScanContext>
{
    public override string Name =>
        nameof(ClassifyFrameWorkflowStep);

    public override Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        context.State.FrameType =
            MagicCardFrameType.OldBorder;

        return Task.CompletedTask;
    }
}