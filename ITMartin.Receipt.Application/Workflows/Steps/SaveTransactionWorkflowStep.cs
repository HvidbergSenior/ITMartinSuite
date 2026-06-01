using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Receipt.Application.Models;

namespace ITMartin.Receipt.Application.Workflows.Steps;

public sealed class SaveTransactionWorkflowStep
    : WorkflowStep<ReceiptContext>
{
    public override string Name =>
        nameof(SaveTransactionWorkflowStep);

    public override Task ExecuteAsync(
        WorkflowExecutionContext<ReceiptContext> context,
        CancellationToken cancellationToken = default)
    {
        var extraction =
            context.State.ExtractionResult
            ?? throw new InvalidOperationException(
                "Extraction result missing.");

        context.State.Transaction =
            new ReceiptTransaction
            {
                Id = Guid.NewGuid(),

                MerchantName =
                    extraction.MerchantName ?? string.Empty,

                PurchaseDate =
                    extraction.PurchaseDate,

                TotalAmount =
                    extraction.TotalAmount,

                VatAmount =
                    extraction.VatAmount,

                Currency =
                    extraction.Currency ?? "DKK",

                Items =
                    extraction.Items
                        .Select(
                            x => new ReceiptTransactionItem
                            {
                                Description =
                                    x.Description,

                                Amount =
                                    x.Amount
                            })
                        .ToList()
            };

        return Task.CompletedTask;
    }
}