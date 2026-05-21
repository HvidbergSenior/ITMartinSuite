namespace ITMartin.Magic.Application.Models;

public sealed class OcrTextResult
{
    public string? Title { get; init; }

    public string? SetCode { get; init; }

    public string? CollectorNumber { get; init; }

    public string? Artist { get; init; }

    public string? BottomText { get; init; }

    public double Confidence { get; init; }
}