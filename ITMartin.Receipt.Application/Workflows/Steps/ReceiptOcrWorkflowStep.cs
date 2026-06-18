using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.OCR.Interfaces;

namespace ITMartin.Receipt.Application.Workflows.Steps;

public sealed class ReceiptOcrWorkflowStep
    : WorkflowStep<ReceiptContext>
{
    private readonly
        IGeneralOcrService
        _ocrService;

    public override string Name =>
        nameof(ReceiptOcrWorkflowStep);

    public ReceiptOcrWorkflowStep(
        IGeneralOcrService ocrService)
    {
        _ocrService =
            ocrService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<ReceiptContext> context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var text = await _ocrService
                .ExtractTextAsync(
                    context.State.ImagePath,
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(text))
                context.State.OcrText = text;
        }
        catch
        {
            // OCR unavailable — AI step will use image directly
        }
    }
}