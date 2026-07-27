using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

public interface IShelfScanService
{
    Task<ShelfScanResult>
        AnalyzeAsync(
            List<string> base64Images,
            CancellationToken cancellationToken = default);
}
