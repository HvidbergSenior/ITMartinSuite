namespace ITMartin.Media.Application.Events.Base;

public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}