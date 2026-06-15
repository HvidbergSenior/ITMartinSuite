namespace ITMartin.OCR.Models;

public sealed class OcrHintResult
{
    public string? Title { get; init; }

    public float TitleConfidence { get; init; }

    public string? SetCode { get; init; }

    public float SetConfidence { get; init; }
}