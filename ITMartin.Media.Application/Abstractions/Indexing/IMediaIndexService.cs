using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;

namespace ITMartin.Media.Application.Abstractions.Indexing;

public interface IMediaIndexService
{
    Task IndexAsync(
        MediaFile mediaFile,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MediaIndex>>
        SearchAsync(
            string query,
            CancellationToken cancellationToken);
}