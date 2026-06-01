using ITMartin.Ai.Models;
using ITMartin.Receipt.Application.Models;

namespace ITMartin.Receipt.Application.Interfaces;

public interface IReceiptExtractionService
{
    Task<ReceiptExtractionResult>
        ExtractAsync(
            string receiptText,
            CancellationToken cancellationToken = default);
}