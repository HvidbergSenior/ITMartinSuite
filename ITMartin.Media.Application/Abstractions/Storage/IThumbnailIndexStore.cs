namespace ITMartin.Media.Application.Abstractions.Storage;

using ITMartin.Media.Application.Models.Storage;

public interface IThumbnailIndexStore
{
    Task SaveAsync(
        ThumbnailIndexEntry entry,
        CancellationToken cancellationToken);

    Task<ThumbnailIndexEntry?> GetAsync(
        Guid mediaId,
        CancellationToken cancellationToken);
}