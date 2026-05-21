namespace ITMartin.OCR.Models;

public sealed class OcrResult
{
    public IReadOnlyCollection<
            OcrTextRegionResult>
        Regions { get; init; }
        =
        [];
}