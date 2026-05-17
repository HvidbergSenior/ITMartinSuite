namespace ITMartin.Media.Application.Abstractions.Queues;

public interface IQueueTransport<T>
{
    Task PublishAsync(
        T message,
        CancellationToken cancellationToken = default);

    Task<T?> ConsumeAsync(
        CancellationToken cancellationToken = default);
}