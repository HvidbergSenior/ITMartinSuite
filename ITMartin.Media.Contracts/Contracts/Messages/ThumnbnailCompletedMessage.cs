// File: ITMartin.Media.Contracts/Messages/ThumbnailCompletedMessage.cs

namespace ITMartin.Media.Infrastructure.Contracts.Messages;

public sealed record ThumbnailCompletedMessage(
    Guid MediaId,
    string ThumbnailPath);