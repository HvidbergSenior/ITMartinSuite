using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

public interface IThumbnailIndexStore
{
    Task SaveAsync(
        ThumbnailIndexEntry entry,
        CancellationToken cancellationToken);

    Task<ThumbnailIndexEntry?> GetAsync(
        Guid mediaId,
        CancellationToken cancellationToken);
}