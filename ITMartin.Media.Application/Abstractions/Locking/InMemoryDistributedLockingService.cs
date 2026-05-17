namespace ITMartin.Media.Infrastructure.Locking;

using System.Collections.Concurrent;
using ITMartin.Media.Application.Abstractions.Locking;

public sealed class InMemoryDistributedLockService
    : IDistributedLockService
{
    private static readonly ConcurrentDictionary<string, bool>
        Locks = new();

    public Task<bool> AcquireAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var acquired =
            Locks.TryAdd(
                key,
                true);

        return Task.FromResult(acquired);
    }

    public Task ReleaseAsync(
        string key,
        CancellationToken cancellationToken)
    {
        Locks.TryRemove(
            key,
            out _);

        return Task.CompletedTask;
    }
}