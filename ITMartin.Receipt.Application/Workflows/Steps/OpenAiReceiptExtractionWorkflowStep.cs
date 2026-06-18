using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Receipt.Application.Interfaces;

namespace ITMartin.Receipt.Application.Workflows.Steps;

public sealed class AiReceiptExtractionWorkflowStep
    : WorkflowStep<ReceiptContext>
{
    private readonly IReceiptExtractionService _receiptExtractionService;
    private readonly IReceiptRepository _repository;

    public override string Name =>
        nameof(AiReceiptExtractionWorkflowStep);

    public AiReceiptExtractionWorkflowStep(
        IReceiptExtractionService receiptExtractionService,
        IReceiptRepository repository)
    {
        _receiptExtractionService = receiptExtractionService;
        _repository = repository;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<ReceiptContext> context,
        CancellationToken cancellationToken = default)
    {
        ReceiptExtractionResult? result;

        if (!string.IsNullOrWhiteSpace(context.State.OcrText))
        {
            result = await _receiptExtractionService.ExtractAsync(
                context.State.OcrText,
                cancellationToken);
        }
        else
        {
            // First pass: extract without template to get merchant name
            var initial = await _receiptExtractionService.ExtractFromImageAsync(
                context.State.ImagePath,
                null,
                cancellationToken);

            // Look up a template for this merchant and re-extract if found
            ReceiptExtractionResult? template = null;
            if (!string.IsNullOrWhiteSpace(initial.MerchantName))
            {
                var templateTx = await _repository.GetTemplateAsync(
                    initial.MerchantName,
                    cancellationToken);

                if (templateTx is not null)
                {
                    template = new ReceiptExtractionResult
                    {
                        MerchantName = templateTx.MerchantName,
                        PurchaseDate = templateTx.PurchaseDate?.ToString("yyyy-MM-dd"),
                        TotalAmount = templateTx.TotalAmount,
                        VatAmount = templateTx.VatAmount,
                        Currency = templateTx.Currency,
                        Items = templateTx.Items
                            .Select(i => new ReceiptLineItem
                            {
                                Description = i.Description,
                                Amount = i.OriginalPrice
                            })
                            .ToList()
                    };
                }
            }

            result = template is not null
                ? await _receiptExtractionService.ExtractFromImageAsync(
                    context.State.ImagePath,
                    template,
                    cancellationToken)
                : initial;
        }

        context.State.ExtractionResult = result;
    }
}
