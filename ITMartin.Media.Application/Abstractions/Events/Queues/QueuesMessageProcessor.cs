using ITMartin.Media.Application.Events.Base;

namespace ITMartin.Media.Application.Abstractions.Events.Queues;

public sealed record QueueMessageProcessedEvent(
    Guid EventId,
    Guid MessageId,
    string Queue,
    DateTimeOffset OccurredAt)
    : IDomainEvent;