using ITMartinLibrary.Application.Workflows;

namespace ITMartinLibrary.Application.Interfaces;

public interface IShelfScanOrchestrator
{
    Task<ShelfScanContext> ExecuteAsync(
        string imagePath,
        CancellationToken cancellationToken);
}
