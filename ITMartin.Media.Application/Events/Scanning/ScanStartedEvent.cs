using ITMartin.Media.Application.Events.Base;

namespace ITMartin.Media.Application.Events.Scanning;

public sealed record ScanStartedEvent(
    Guid EventId,
    Guid SessionId,
    string RootPath,
    DateTimeOffset OccurredAt) : IDomainEvent;