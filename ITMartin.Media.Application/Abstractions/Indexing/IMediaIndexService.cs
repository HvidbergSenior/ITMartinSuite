namespace ITMartin.Media.Application.Abstractions.Indexing;

using ITMartin.Media.Domain.Entities;

public interface IMediaIndexService
{
    Task IndexAsync(
        MediaFile mediaFile,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MediaIndexEntity>>
        SearchAsync(
            string query,
            CancellationToken cancellationToken);
}