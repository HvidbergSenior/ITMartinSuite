namespace ITMartin.OCR.Models;

public sealed class OcrTextRegionResult
{
    public required string RegionName { get; init; }

    public string? Text { get; init; }

    public double Confidence { get; init; }
}