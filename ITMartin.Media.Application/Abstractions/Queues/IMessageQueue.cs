namespace ITMartin.Media.Application.Abstractions.Queues;

using ITMartin.Media.Application.Queues.Models;

public interface IMessageQueue
{
    Task EnqueueAsync(
        QueueMessage message,
        CancellationToken cancellationToken);

    Task<QueueMessage?> DequeueAsync(
        string queue,
        CancellationToken cancellationToken);
}