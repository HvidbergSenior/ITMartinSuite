namespace ITMartin.Media.Application.Events.BackgroundJobs;

using ITMartin.Media.Application.Events.Base;

public sealed record BackgroundJobCompletedEvent(
    Guid EventId,
    Guid JobId,
    string Queue,
    DateTimeOffset OccurredAt)
    : IDomainEvent;