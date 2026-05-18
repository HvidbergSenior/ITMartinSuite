// File: ITMartin.Media.Application/Abstractions/Queues/IQueueProducer.cs

namespace ITMartin.Media.Application.Abstractions.Queues;

public interface IQueueProducer<T>
{
    Task EnqueueAsync(
        T message,
        CancellationToken cancellationToken = default);
}