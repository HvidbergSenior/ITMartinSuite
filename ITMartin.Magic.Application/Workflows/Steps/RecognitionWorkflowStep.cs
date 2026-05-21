using ITMartin.Magic.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class RecognitionWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        ICardRecognitionService
        _cardRecognitionService;

    public override string Name =>
        nameof(RecognitionWorkflowStep);

    public RecognitionWorkflowStep(
        ICardRecognitionService cardRecognitionService)
    {
        _cardRecognitionService =
            cardRecognitionService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State.OcrResult is null)
        {
            throw new InvalidOperationException(
                "OCR result missing.");
        }

        var result =
            await _cardRecognitionService
                .RecognizeAsync(
                    context.State.OcrResult);

        if (result is null)
        {
            throw new InvalidOperationException(
                "Card recognition failed.");
        }

        context.State.RecognitionResult =
            result;
    }
}