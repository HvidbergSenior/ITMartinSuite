using ITMartin.Magic.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class DetectCardWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        ICardLayoutDetectionService
        _cardLayoutDetectionService;

    public override string Name =>
        nameof(DetectCardWorkflowStep);

    public DetectCardWorkflowStep(
        ICardLayoutDetectionService cardLayoutDetectionService)
    {
        _cardLayoutDetectionService =
            cardLayoutDetectionService;
    }

    public override Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        var result =
            _cardLayoutDetectionService
                .Detect(
                    context.State.ImagePath);

        context.State.LayoutType =
            result;

        return Task.CompletedTask;
    }
}