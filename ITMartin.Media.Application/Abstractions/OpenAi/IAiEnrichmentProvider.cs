// File: ITMartin.Media.Application/Abstractions/OpenAi/IAiEnrichmentProvider.cs

namespace ITMartin.Media.Application.Abstractions.OpenAi;

public interface IAiEnrichmentProvider
{
    Task<AiEnrichmentResult> EnrichAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}