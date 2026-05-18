// File: ITMartin.Media.Application/Abstractions/Queues/IQueueConsumer.cs

namespace ITMartin.Media.Application.Abstractions.Queues;

public interface IQueueConsumer<T>
{
    Task<T> DequeueAsync(
        CancellationToken cancellationToken = default);
}