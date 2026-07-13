using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

public interface IElBillExtractionService
{
    Task<ElBillExtractionResult>
        ExtractFromImageAsync(
            string imagePath,
            CancellationToken cancellationToken = default);
}
