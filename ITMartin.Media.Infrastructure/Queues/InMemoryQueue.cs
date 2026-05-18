// File: ITMartin.Media.Infrastructure/Queues/InMemoryQueue.cs

using System.Threading.Channels;
using ITMartin.Media.Application.Abstractions.Queues;

namespace ITMartin.Media.Infrastructure.Queues;

public sealed class InMemoryQueue<T>
    : IQueueProducer<T>,
        IQueueConsumer<T>
{
    private readonly Channel<T> _channel;

    public InMemoryQueue()
    {
        _channel = Channel.CreateUnbounded<T>();
    }

    public async Task EnqueueAsync(
        T message,
        CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(
            message,
            cancellationToken);
    }

    public async Task<T> DequeueAsync(
        CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(
            cancellationToken);
    }
}