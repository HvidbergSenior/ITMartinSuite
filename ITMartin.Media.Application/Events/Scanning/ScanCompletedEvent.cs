using ITMartin.Media.Application.Events.Base;

namespace ITMartin.Media.Application.Events.Scanning;

public sealed record ScanCompletedEvent(
    Guid EventId,
    Guid SessionId,
    long FilesProcessed,
    DateTimeOffset OccurredAt) : IDomainEvent;