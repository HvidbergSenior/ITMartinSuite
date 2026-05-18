// File: ITMartin.Media.Contracts/Messages/OpenAiEnrichmentMessage.cs

namespace ITMartin.Media.Infrastructure.Contracts.Messages;

public sealed record OpenAiEnrichmentMessage(
    Guid MediaId,
    string FilePath,
    string ContentType);