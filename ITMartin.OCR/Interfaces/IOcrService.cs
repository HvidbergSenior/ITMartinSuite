using ITMartin.OCR.Models;

namespace ITMartin.OCR.Interfaces;

public interface IOcrService
{
    Task<OcrResult?> ExtractTextAsync(
        OcrRegionResult regions, CancellationToken cancellationToken);
}