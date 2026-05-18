// File: ITMartin.Media.Application/OpenAi/AiEnrichmentResult.cs

namespace ITMartin.Media.Application.Abstractions.OpenAi;

public sealed class AiEnrichmentResult
{
    public string? Summary { get; init; }

    public IReadOnlyCollection<string> Tags { get; init; } = [];

    public Dictionary<string, string> Metadata { get; init; } = [];
}