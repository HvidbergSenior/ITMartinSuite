using ITMartin.Media.Application.Events.Base;

namespace ITMartin.Media.Application.Abstractions.Events;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken)
        where TEvent : IDomainEvent;
}