using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface IOcrRegionExtractor
{
    Task<OcrRegionResult?> ExtractAsync(
        string normalizedCardPath);
}