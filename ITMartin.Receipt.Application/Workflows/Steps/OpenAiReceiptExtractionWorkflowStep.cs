using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Ai.Interfaces;

namespace ITMartin.Receipt.Application.Workflows.Steps;

public sealed class OpenAiReceiptExtractionWorkflowStep
    : WorkflowStep<ReceiptContext>
{
    private readonly
        IReceiptExtractionService
        _receiptExtractionService;

    public override string Name =>
        nameof(OpenAiReceiptExtractionWorkflowStep);

    public OpenAiReceiptExtractionWorkflowStep(
        IReceiptExtractionService receiptExtractionService)
    {
        _receiptExtractionService =
            receiptExtractionService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<ReceiptContext> context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                context.State.OcrText))
        {
            throw new InvalidOperationException(
                "OCR text missing.");
        }

        var result =
            await _receiptExtractionService
                .ExtractAsync(
                    context.State.OcrText,
                    cancellationToken);

        context.State.ExtractionResult =
            result;
    }
}