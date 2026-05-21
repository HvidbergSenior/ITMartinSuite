using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Workflows.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows;

public sealed class RecognitionWorkflowStep
    : IWorkflowStep
{
    private readonly
        ICardRecognitionService
        _cardRecognitionService;
    public string Name =>
        nameof(DetectCardWorkflowStep);

    public Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default) where TState : class
    {
        throw new NotImplementedException();
    }

    public RecognitionWorkflowStep(
        ICardRecognitionService
            cardRecognitionService)
    {
        _cardRecognitionService =
            cardRecognitionService;
    }

    public async Task ExecuteAsync(
        CardScanContext context,
        CancellationToken cancellationToken)
    {
        if (context.OcrResult is null)
        {
            context.Fail(
                "OCR result missing.");

            return;
        }

        var result =
            await _cardRecognitionService
                .RecognizeAsync(
                    context.OcrResult);

        if (result is null)
        {
            context.Fail(
                "Card recognition failed.");

            return;
        }

        context.CaptureResult =
            result;
    }
}