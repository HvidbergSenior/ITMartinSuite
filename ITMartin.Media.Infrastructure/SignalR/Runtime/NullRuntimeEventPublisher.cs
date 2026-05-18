using ITMartin.Media.Application.Abstractions.Runtime;

namespace ITMartin.Media.Infrastructure.SignalR.Runtime;

public sealed class NullRuntimeEventPublisher
    : IRuntimeEventPublisher
{
    public Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
        where T : class
    {
        return Task.CompletedTask;
    }
}