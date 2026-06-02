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
        ArgumentNullException.ThrowIfNull(
            context.State.DetectedCardImagePath);

        if (context.State.CardCornerResult is null)
        {
            context.State.PerspectiveCorrectedImagePath =
                context.State.DetectedCardImagePath;

            return;
        }

        var correctedImagePath =
            await _perspectiveCorrectionService
                .CorrectAsync(
                    context.State.DetectedCardImagePath,
                    context.State.CardCornerResult,
                    cancellationToken);

        context.State.PerspectiveCorrectedImagePath =
            string.IsNullOrWhiteSpace(correctedImagePath)
                ? context.State.DetectedCardImagePath
                : correctedImagePath;
    }
}