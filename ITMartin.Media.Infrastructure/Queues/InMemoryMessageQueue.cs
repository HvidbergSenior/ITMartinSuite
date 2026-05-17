namespace ITMartin.Media.Infrastructure.Queues;

using System.Collections.Concurrent;
using ITMartin.Media.Application.Abstractions.Queues;
using ITMartin.Media.Application.Queues.Models;

public sealed class InMemoryMessageQueue
    : IMessageQueue
{
    private readonly ConcurrentQueue<QueueMessage>
        _queue = new();

    public Task EnqueueAsync(
        QueueMessage message,
        CancellationToken cancellationToken)
    {
        _queue.Enqueue(message);

        return Task.CompletedTask;
    }

    public Task<QueueMessage?> DequeueAsync(
        string queue,
        CancellationToken cancellationToken)
    {
        _queue.TryDequeue(out var message);

        return Task.FromResult(message);
    }
}