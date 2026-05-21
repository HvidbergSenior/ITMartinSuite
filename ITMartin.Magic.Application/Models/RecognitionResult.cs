namespace ITMartin.Magic.Application.Models;

public sealed record RecognitionResult
{
    public string? CardName { get; init; }

    public string? SetCode { get; init; }

    public string? CollectorNumber { get; init; }
}