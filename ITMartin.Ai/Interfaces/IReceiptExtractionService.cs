using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

public interface IReceiptExtractionService
{
    Task<ReceiptExtractionResult>
        ExtractAsync(
            string receiptText,
            ReceiptExtractionResult? template = null,
            CancellationToken cancellationToken = default);

    Task<ReceiptExtractionResult>
        ExtractFromImageAsync(
            List<string> imagePaths,
            ReceiptExtractionResult? template = null,
            CancellationToken cancellationToken = default);
}