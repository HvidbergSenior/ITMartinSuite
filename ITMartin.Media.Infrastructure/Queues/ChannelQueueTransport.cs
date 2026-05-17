using System.Threading.Channels;
using ITMartin.Media.Application.Abstractions.Queues;

namespace ITMartin.Media.Infrastructure.Queues;

public sealed class ChannelQueueTransport<T>
    : IQueueTransport<T>
{
    private readonly Channel<T> _channel;

    public ChannelQueueTransport()
    {
        _channel = Channel.CreateUnbounded<T>();
    }

    public async Task PublishAsync(
        T message,
        CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(message, cancellationToken);
    }

    public async Task<T?> ConsumeAsync(
        CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}