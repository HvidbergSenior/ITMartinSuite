using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Application.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartinLibrary.Application.Workflows.Steps;

public sealed class ItemLookupWorkflowStep
    : WorkflowStep<ShelfScanContext>
{
    private readonly IBarcodeLookupService _barcodeLookupService;

    private readonly ILogger<ItemLookupWorkflowStep> _logger;

    public override string Name =>
        nameof(ItemLookupWorkflowStep);

    public ItemLookupWorkflowStep(
        IBarcodeLookupService barcodeLookupService,
        ILogger<ItemLookupWorkflowStep> logger)
    {
        _barcodeLookupService = barcodeLookupService;
        _logger = logger;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<ShelfScanContext> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State.AiResult is null)
        {
            return;
        }

        foreach (var item in context.State.AiResult.Items)
        {
            var barcode = item.Barcode ?? item.Isbn;

            if (string.IsNullOrWhiteSpace(barcode))
            {
                continue;
            }

            try
            {
                var inventoryItem =
                    await _barcodeLookupService.LookupAsync(barcode);

                if (inventoryItem is not null &&
                    !string.IsNullOrWhiteSpace(inventoryItem.CoverUrl))
                {
                    _logger.LogDebug(
                        "Barcode lookup hit — {Barcode} → {Title}",
                        barcode,
                        inventoryItem.Title);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Barcode lookup failed for {Barcode}",
                    barcode);
            }
        }
    }
}
