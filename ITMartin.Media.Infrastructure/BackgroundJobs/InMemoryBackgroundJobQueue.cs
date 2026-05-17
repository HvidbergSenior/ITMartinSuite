using System.Collections.Concurrent;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;

namespace ITMartin.Media.Infrastructure.BackgroundJobs;

public sealed class InMemoryBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly ConcurrentQueue<BackgroundJob> _queue = new();

    public Task EnqueueAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        _queue.Enqueue(job);

        return Task.CompletedTask;
    }

    public Task<BackgroundJob?> DequeueAsync(
        string queue,
        CancellationToken cancellationToken)
    {
        _queue.TryDequeue(out var job);

        return Task.FromResult(job);
    }
}