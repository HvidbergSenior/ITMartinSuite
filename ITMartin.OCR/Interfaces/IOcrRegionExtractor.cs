using ITMartin.OCR.Models;

namespace ITMartin.OCR.Interfaces;

public interface IOcrRegionExtractor
{
    Task<OcrRegionResult?> ExtractAsync(
        string normalizedCardPath);
}