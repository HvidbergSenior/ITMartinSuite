namespace ITMartin.Ai.Models;

public sealed record CdRecognitionResult
{
    public List<RecognizedCd> Cds { get; init; } = [];
}
