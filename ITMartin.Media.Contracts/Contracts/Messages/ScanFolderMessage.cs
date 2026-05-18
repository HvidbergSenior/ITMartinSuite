// File: ITMartin.Media.Contracts/Messages/ScanFolderMessage.cs

namespace ITMartin.Media.Infrastructure.Contracts.Messages;

public sealed record ScanFolderMessage(
    Guid ScanSessionId,
    string Path,
    bool Recursive);