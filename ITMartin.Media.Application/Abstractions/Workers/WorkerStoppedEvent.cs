using ITMartin.Media.Application.Events.Base;

namespace ITMartin.Media.Application.Abstractions.Workers;

public sealed record WorkerStoppedEvent(
    Guid EventId,
    string WorkerName,
    DateTimeOffset OccurredAt)
    : IDomainEvent;