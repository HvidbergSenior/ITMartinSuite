// File: ITMartin.Media.Contracts/Runtime/ThumbnailGeneratedEvent.cs

namespace ITMartin.Media.Infrastructure.Contracts.Runtime;

public sealed record ThumbnailGeneratedEvent(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    string NodeId,
    Guid MediaId,
    string ThumbnailPath)
    : IWorkerRuntimeEvent;