using ITMartin.Media.Application.Events.Base;

namespace ITMartin.Media.Application.Events.Scanning;

public sealed record ScanFailedEvent(
    Guid EventId,
    Guid SessionId,
    string Error,
    DateTimeOffset OccurredAt) : IDomainEvent;