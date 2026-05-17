using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

namespace ITMartin.Media.Application.Services;

using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Persistence;
using ITMartin.Media.Application.Events.BackgroundJobs;

public sealed class BackgroundJobService
{
    private readonly IBackgroundJobQueue _queue;
    private readonly IBackgroundJobRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public BackgroundJobService(
        IBackgroundJobQueue queue,
        IBackgroundJobRepository repository,
        IEventPublisher eventPublisher)
    {
        _queue = queue;
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    public async Task<Guid> EnqueueAsync<T>(
        string queue,
        T payload,
        CancellationToken cancellationToken)
    {
        var job =
            new BackgroundJob
            {
                Id = Guid.NewGuid(),
                Queue = queue,
                Type = typeof(T).Name,
                Payload =
                    JsonSerializer.Serialize(payload),
                Status = "Pending",
                CreatedAt = DateTimeOffset.UtcNow
            };

        await _repository.CreateAsync(
            job,
            cancellationToken);

        await _queue.EnqueueAsync(
            job,
            cancellationToken);

        await _eventPublisher.PublishAsync(
            new BackgroundJobQueuedEvent(
                Guid.NewGuid(),
                job.Id,
                queue,
                job.Type,
                DateTimeOffset.UtcNow),
            cancellationToken);

        return job.Id;
    }
}