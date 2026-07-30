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
        var hasOcrText = !string.IsNullOrWhiteSpace(context.State.OcrText);
        var imagePaths = new List<string> { context.State.ImagePath };
        imagePaths.AddRange(context.State.AdditionalImagePaths);

        // First pass: identify the merchant, with no reference yet.
        var initial = hasOcrText
            ? await _receiptExtractionService.ExtractAsync(context.State.OcrText!, null, cancellationToken)
            : await _receiptExtractionService.ExtractFromImageAsync(imagePaths, null, cancellationToken);

        var reference = !string.IsNullOrWhiteSpace(initial.MerchantName)
            ? await _repository.GetReferenceAsync(initial.MerchantName, cancellationToken)
            : null;

        if (reference is null)
        {
            context.State.ExtractionResult = initial;
            return;
        }

        // A learned reference exists for this merchant from a previous, internally-
        // consistent scan - re-extract using it to calibrate this store's quirks
        // (quantity layouts, discount wording, loyalty section, etc.) automatically.
        var template = new ReceiptExtractionResult
        {
            MerchantName = reference.MerchantName,
            PurchaseDate = reference.PurchaseDate?.ToString("yyyy-MM-dd"),
            TotalAmount = reference.TotalAmount,
            VatAmount = reference.VatAmount,
            Currency = reference.Currency,
            Items = reference.Items
                .Select(i => new ReceiptLineItem
                {
                    Description = i.Description,
                    Amount = i.OriginalPrice,
                    DiscountAmount = i.DiscountAmount,
                    DiscountLabel = i.DiscountType
                })
                .ToList()
        };

        var refined = hasOcrText
            ? await _receiptExtractionService.ExtractAsync(context.State.OcrText!, template, cancellationToken)
            : await _receiptExtractionService.ExtractFromImageAsync(imagePaths, template, cancellationToken);

        context.State.ExtractionResult = refined;
    }
}
