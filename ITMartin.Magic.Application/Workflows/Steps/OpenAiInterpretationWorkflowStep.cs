using ITMartin.Ai.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class OpenAiInterpretationWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        IMagicCardRecognitionService
        _magicCardRecognitionService;

    public override string Name =>
        nameof(OpenAiInterpretationWorkflowStep);

    public OpenAiInterpretationWorkflowStep(
        IMagicCardRecognitionService magicCardRecognitionService)
    {
        _magicCardRecognitionService =
            magicCardRecognitionService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State.PerspectiveCorrectedImagePath is null)
        {
            throw new InvalidOperationException(
                "Perspective corrected image missing.");
        }

        if (context.State.DetectionResult is null)
        {
            throw new InvalidOperationException(
                "Detection result missing.");
        }

        var result =
            await _magicCardRecognitionService
                .AnalyzeAsync(
                    context.State.PerspectiveCorrectedImagePath,
                    context.State.DetectionResult);

        if (result is null)
        {
            return;
        }

        context.State.OpenAiResult =
            result;
    }
}