using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Receipt.Application.Interfaces;
using ITMartin.Receipt.Application.Models;
using DomainEntities = ITMartin.Receipt.Domain.Entities;

namespace ITMartin.Receipt.Application.Workflows.Steps;

public sealed class SaveTransactionWorkflowStep
    : WorkflowStep<ReceiptContext>
{
    private readonly IReceiptRepository _repository;

    public SaveTransactionWorkflowStep(IReceiptRepository repository)
    {
        _repository = repository;
    }

    public override string Name =>
        nameof(SaveTransactionWorkflowStep);

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<ReceiptContext> context,
        CancellationToken cancellationToken = default)
    {
        var extraction =
            context.State.ExtractionResult
            ?? throw new InvalidOperationException(
                "Extraction result missing.");

        var id = Guid.NewGuid();

        var appItems = extraction.Items
            .Select(line =>
            {
                var netPrice = (line.Amount ?? 0) + (line.DiscountAmount ?? 0);

                return new ReceiptTransactionItem
                {
                    Description    = line.Description,
                    OriginalPrice  = line.Amount,
                    DiscountAmount = line.DiscountAmount,
                    DiscountType   = line.DiscountLabel,
                    // A negative net price means the AI got the original/discount reversed
                    // (e.g. an already-discounted printed amount treated as the pre-discount price).
                    IsSuspicious   = line.Suspicious || (line.DiscountAmount.HasValue && netPrice < 0),
                };
            })
            .ToList();

        var purchaseDate = DateTime.TryParse(extraction.PurchaseDate, out var d) ? d : (DateTime?)null;
        var imageFileName = Path.GetFileName(context.State.ImagePath);

        context.State.Transaction =
            new ReceiptTransaction
            {
                Id = id,
                MerchantName = extraction.MerchantName ?? string.Empty,
                PurchaseDate = purchaseDate,
                TotalAmount = extraction.TotalAmount,
                VatAmount = extraction.VatAmount,
                Currency = extraction.Currency ?? "DKK",
                Items = appItems,
                ImageFileName = imageFileName
            };

        var domainTransaction =
            new DomainEntities.ReceiptTransaction
            {
                Id = id,
                MerchantName = extraction.MerchantName ?? string.Empty,
                PurchaseDate = purchaseDate,
                TotalAmount = extraction.TotalAmount,
                VatAmount = extraction.VatAmount,
                Currency = extraction.Currency ?? "DKK",
                Items = appItems
                    .Select(x => new DomainEntities.ReceiptTransactionItem
                    {
                        Description    = x.Description,
                        OriginalPrice  = x.OriginalPrice,
                        DiscountAmount = x.DiscountAmount,
                        DiscountType   = x.DiscountType,
                        IsSuspicious   = x.IsSuspicious
                    })
                    .ToList(),
                ImageFileName = imageFileName
            };

        await _repository.SaveAsync(domainTransaction, cancellationToken);
    }
}
