using ITMartin.Media.Application.Events.Base;

namespace ITMartin.Media.Application.Abstractions.Events.Queues;

public sealed record QueueMessageFailedEvent(
    Guid EventId,
    Guid MessageId,
    string Queue,
    string Error,
    DateTimeOffset OccurredAt)
    : IDomainEvent;