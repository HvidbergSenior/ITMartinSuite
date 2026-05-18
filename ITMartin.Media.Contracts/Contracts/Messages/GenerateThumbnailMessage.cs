// File: ITMartin.Media.Contracts/Messages/GenerateThumbnailMessage.cs

namespace ITMartin.Media.Infrastructure.Contracts.Messages;

public sealed record GenerateThumbnailMessage(
    Guid MediaId,
    string FilePath,
    string OutputPath);