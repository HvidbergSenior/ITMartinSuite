using ITMartin.Media.Application.Events.Base;

namespace ITMartin.Media.Application.Abstractions.Events;

public interface IEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(
        TEvent domainEvent,
        CancellationToken cancellationToken);
}