// File: ITMartin.Media.Contracts/Messages/AiEnrichmentCompletedMessage.cs

namespace ITMartin.Media.Infrastructure.Contracts.Messages;

public sealed record AiEnrichmentCompletedMessage(
    Guid MediaId,
    string Summary,
    IReadOnlyCollection<string> Tags);