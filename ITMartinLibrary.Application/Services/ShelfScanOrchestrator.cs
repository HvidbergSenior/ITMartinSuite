using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Application.Workflows;

namespace ITMartinLibrary.Application.Services;

public sealed class ShelfScanOrchestrator
    : IShelfScanOrchestrator
{
    private readonly ShelfScanWorkflowRunner _runner;

    public ShelfScanOrchestrator(ShelfScanWorkflowRunner runner)
    {
        _runner = runner;
    }

    public async Task<ShelfScanContext> ExecuteAsync(
        string imagePath,
        CancellationToken cancellationToken)
    {
        var context =
            new ShelfScanContext
            {
                ImagePath = imagePath
            };

        try
        {
            await _runner.ExecuteAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            context.Fail(ex.Message);
        }

        return context;
    }
}
