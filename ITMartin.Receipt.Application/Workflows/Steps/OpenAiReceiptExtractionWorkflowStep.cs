using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Ai.Interfaces;

namespace ITMartin.Receipt.Application.Workflows.Steps;

public sealed class AiReceiptExtractionWorkflowStep
    : WorkflowStep<ReceiptContext>
{
    private readonly
        IReceiptExtractionService
        _receiptExtractionService;

    public override string Name =>
        nameof(AiReceiptExtractionWorkflowStep);

    public AiReceiptExtractionWorkflowStep(
        IReceiptExtractionService receiptExtractionService)
    {
        _receiptExtractionService =
            receiptExtractionService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<ReceiptContext> context,
        CancellationToken cancellationToken = default)
    {
        var result = string.IsNullOrWhiteSpace(context.State.OcrText)
            ? await _receiptExtractionService.ExtractFromImageAsync(
                context.State.ImagePath,
                cancellationToken)
            : await _receiptExtractionService.ExtractAsync(
                context.State.OcrText,
                cancellationToken);

        context.State.ExtractionResult = result;
    }
}