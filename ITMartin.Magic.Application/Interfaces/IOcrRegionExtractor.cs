using ITMartin.Magic.Application.Models;
using ITMartin.OCR.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface IOcrRegionExtractor
{
    Task<OcrRegionResult?> ExtractAsync(
        string normalizedCardPath);
}