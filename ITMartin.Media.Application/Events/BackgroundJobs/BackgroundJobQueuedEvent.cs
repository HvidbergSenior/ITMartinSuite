namespace ITMartin.Media.Application.Events.BackgroundJobs;

using ITMartin.Media.Application.Events.Base;

public sealed record BackgroundJobQueuedEvent(
    Guid EventId,
    Guid JobId,
    string Queue,
    string Type,
    DateTimeOffset OccurredAt)
    : IDomainEvent;