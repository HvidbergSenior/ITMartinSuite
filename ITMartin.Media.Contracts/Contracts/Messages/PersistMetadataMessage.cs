// File: ITMartin.Media.Contracts/Messages/PersistMetadataMessage.cs

namespace ITMartin.Media.Infrastructure.Contracts.Messages;

public sealed record PersistMetadataMessage(
    Guid MediaId,
    string FilePath,
    Dictionary<string, string> Metadata);