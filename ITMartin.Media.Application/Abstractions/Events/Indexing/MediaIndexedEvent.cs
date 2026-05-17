using ITMartin.Media.Application.Events.Base;

namespace ITMartin.Media.Application.Abstractions.Events.Indexing;

public sealed record MediaIndexedEvent(
    Guid EventId,
    Guid MediaId,
    string FileName,
    DateTimeOffset OccurredAt)
    : IDomainEvent;