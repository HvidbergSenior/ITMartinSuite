using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Events.Base;

namespace ITMartin.Media.Infrastructure.Events;

public sealed class NullEventPublisher
    : IEventPublisher
{
    public Task PublishAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        return Task.CompletedTask;
    }
}