using ITMartin.Magic.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class PerspectiveCorrectionWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        IPerspectiveCorrectionService
        _perspectiveCorrectionService;

    public override string Name =>
        nameof(PerspectiveCorrectionWorkflowStep);

    public PerspectiveCorrectionWorkflowStep(
        IPerspectiveCorrectionService perspectiveCorrectionService)
    {
        _perspectiveCorrectionService =
            perspectiveCorrectionService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State.DetectedCardImagePath is null)
        {
            throw new InvalidOperationException(
                "Detected card image missing.");
        }

        if (context.State.CardCornerResult is null)
        {
            throw new InvalidOperationException(
                "Card corners were not detected.");
        }

        var correctedImagePath =
            await _perspectiveCorrectionService
                .CorrectAsync(
                    context.State.DetectedCardImagePath,
                    context.State.CardCornerResult);

        if (string.IsNullOrWhiteSpace(
                correctedImagePath))
        {
            throw new InvalidOperationException(
                "Perspective correction failed.");
        }

        context.State.PerspectiveCorrectedImagePath =
            correctedImagePath;
    }
}