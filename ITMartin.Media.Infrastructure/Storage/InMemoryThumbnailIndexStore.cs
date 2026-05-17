namespace ITMartin.Media.Infrastructure.Storage;

using ITMartin.Media.Application.Abstractions.Storage;
using ITMartin.Media.Application.Models.Storage;

public sealed class InMemoryThumbnailIndexStore
    : IThumbnailIndexStore
{
    private static readonly Dictionary<Guid,
            ThumbnailIndexEntry>
        Entries = [];

    public Task SaveAsync(
        ThumbnailIndexEntry entry,
        CancellationToken cancellationToken)
    {
        Entries[entry.MediaId] = entry;

        return Task.CompletedTask;
    }

    public Task<ThumbnailIndexEntry?> GetAsync(
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        Entries.TryGetValue(
            mediaId,
            out var entry);

        return Task.FromResult(entry);
    }
}