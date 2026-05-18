// File: ITMartin.Media.Application/Abstractions/Runtime/IRuntimeEventPublisher.cs

namespace ITMartin.Media.Application.Abstractions.Runtime;

public interface IRuntimeEventPublisher
{
    Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
        where T : class;
}