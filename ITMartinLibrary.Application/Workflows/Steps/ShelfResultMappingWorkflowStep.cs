using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartinLibrary.Application.Models;
using ITMartinLibrary.Application.Workflows;

namespace ITMartinLibrary.Application.Workflows.Steps;

public sealed class ShelfResultMappingWorkflowStep
    : WorkflowStep<ShelfScanContext>
{
    public override string Name =>
        nameof(ShelfResultMappingWorkflowStep);

    public override Task ExecuteAsync(
        WorkflowExecutionContext<ShelfScanContext> context,
        CancellationToken cancellationToken = default)
    {
        var items = context.State.AiResult?.Items ?? [];

        context.State.Result =
            new ShelfScanResult
            {
                Items = items
                    .Select(x => new ScannedShelfItem
                    {
                        Title = x.Title,
                        Author = x.Author,
                        Barcode = x.Barcode,
                        Isbn = x.Isbn,
                        MediaType = x.MediaType ?? "Unknown",
                        Confidence = x.Confidence
                    })
                    .ToList()
            };

        return Task.CompletedTask;
    }
}
