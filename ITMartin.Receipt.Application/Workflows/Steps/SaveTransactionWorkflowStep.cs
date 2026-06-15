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
            .Select(x => new ReceiptTransactionItem
            {
                Description = x.Description,
                Amount = x.Amount
            })
            .ToList();

        context.State.Transaction =
            new ReceiptTransaction
            {
                Id = id,
                MerchantName = extraction.MerchantName ?? string.Empty,
                PurchaseDate = extraction.PurchaseDate,
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
                PurchaseDate = extraction.PurchaseDate,
                TotalAmount = extraction.TotalAmount,
                VatAmount = extraction.VatAmount,
                Currency = extraction.Currency ?? "DKK",
                Items = appItems
                    .Select(x => new DomainEntities.ReceiptTransactionItem
                    {
                        Description = x.Description,
                        Amount = x.Amount
                    })
                    .ToList()
            };

        await _repository.SaveAsync(domainTransaction, cancellationToken);
    }
}
