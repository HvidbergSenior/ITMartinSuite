using System.Collections.Concurrent;
using ITMartin.Media.Application.Abstractions.Distributed;

namespace ITMartin.Media.Infrastructure.Distributed;

public sealed class InMemoryDistributedCoordinator
    : IDistributedCoordinator
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _locks = new();

    public Task<bool> TryAcquireAsync(
        string resource,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(duration);

        var success = _locks.TryAdd(resource, expiresAt);

        return Task.FromResult(success);
    }

    public Task ReleaseAsync(
        string resource,
        CancellationToken cancellationToken = default)
    {
        _locks.TryRemove(resource, out _);

        return Task.CompletedTask;
    }
}