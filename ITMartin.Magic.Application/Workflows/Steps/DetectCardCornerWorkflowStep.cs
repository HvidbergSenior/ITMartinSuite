using ITMartin.Magic.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class DetectCardCornersWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        ICardCornerDetectionService
        _cardCornerDetectionService;

    public override string Name =>
        nameof(DetectCardCornersWorkflowStep);

    public DetectCardCornersWorkflowStep(
        ICardCornerDetectionService cardCornerDetectionService)
    {
        _cardCornerDetectionService =
            cardCornerDetectionService;
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

        var result =
            await _cardCornerDetectionService
                .DetectAsync(
                    context.State.DetectedCardImagePath);

        if (result is null)
        {
            throw new InvalidOperationException(
                "Card corner detection failed.");
        }

        context.State.CardCornerResult =
            result;
    }
}