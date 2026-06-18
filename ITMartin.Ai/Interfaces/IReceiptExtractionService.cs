using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

public interface IReceiptExtractionService
{
    Task<ReceiptExtractionResult>
        ExtractAsync(
            string receiptText,
            CancellationToken cancellationToken = default);

    Task<ReceiptExtractionResult>
        ExtractFromImageAsync(
            string imagePath,
            CancellationToken cancellationToken = default);
}