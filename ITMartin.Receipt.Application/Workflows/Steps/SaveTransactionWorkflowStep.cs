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

        var appItems = PairItems(extraction.Items);

        var purchaseDate = DateTime.TryParse(extraction.PurchaseDate, out var d) ? d : (DateTime?)null;

        context.State.Transaction =
            new ReceiptTransaction
            {
                Id = id,
                MerchantName = extraction.MerchantName ?? string.Empty,
                PurchaseDate = purchaseDate,
                TotalAmount = extraction.TotalAmount,
                VatAmount = extraction.VatAmount,
                Currency = extraction.Currency ?? "DKK",
                Items = appItems
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
                        DiscountType   = x.DiscountType
                    })
                    .ToList()
            };

        await _repository.SaveAsync(domainTransaction, cancellationToken);
    }

    // If a line is a discount keyword line, merge it with the product above it.
    private static List<ReceiptTransactionItem> PairItems(
        IEnumerable<ITMartin.Ai.Models.ReceiptLineItem> rawLines)
    {
        var result = new List<ReceiptTransactionItem>();

        foreach (var line in rawLines)
        {
            var discountType = DetectDiscountType(line.Description);

            if (discountType != null && result.Count > 0)
            {
                var prev = result[^1];
                prev.DiscountAmount = line.Amount;
                prev.DiscountType   = discountType;
            }
            else
            {
                result.Add(new ReceiptTransactionItem
                {
                    Description   = line.Description,
                    OriginalPrice = line.Amount,
                });
            }
        }

        return result;
    }

    private static string? DetectDiscountType(string description)
    {
        var d = description.ToLowerInvariant();

        if (d.Contains("pluskupon") || d.Contains("plus-kupon") ||
            d.Contains("plus kupon") || d.Contains("lidl plus"))
            return "Plus-kupon";

        if (d.Contains("rabat"))
            return "Rabat";

        return null;
    }
}
