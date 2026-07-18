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
        var template = await ResolveTemplateAsync(context.State.SelectedTemplateId, cancellationToken);

        var result = !string.IsNullOrWhiteSpace(context.State.OcrText)
            ? await _receiptExtractionService.ExtractAsync(
                context.State.OcrText,
                template,
                cancellationToken)
            : await _receiptExtractionService.ExtractFromImageAsync(
                context.State.ImagePath,
                template,
                cancellationToken);

        context.State.ExtractionResult = result;
    }

    private async Task<ReceiptExtractionResult?> ResolveTemplateAsync(
        Guid? selectedTemplateId,
        CancellationToken cancellationToken)
    {
        if (selectedTemplateId is null)
            return null;

        var templateTx = await _repository.GetByIdAsync(selectedTemplateId.Value, cancellationToken);
        if (templateTx is null)
            return null;

        return new ReceiptExtractionResult
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
                    Amount = i.OriginalPrice,
                    DiscountAmount = i.DiscountAmount,
                    DiscountLabel = i.DiscountType
                })
                .ToList()
        };
    }
}
