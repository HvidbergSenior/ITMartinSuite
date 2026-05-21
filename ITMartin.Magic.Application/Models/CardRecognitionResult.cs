namespace ITMartin.Magic.Application.Models;

public sealed record CardRecognitionResult
{
    public string? Name { get; init; }

    public string? SetCode { get; init; }

    public string? CollectorNumber { get; init; }

    public decimal Confidence { get; init; }

    public bool ExactMatch { get; init; }
}