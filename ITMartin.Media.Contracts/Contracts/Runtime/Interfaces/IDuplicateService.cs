using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IDuplicateService
{
        Task<List<DuplicateGroup>> BuildDuplicateGroupsAsync(
            IReadOnlyCollection<MediaFile> files,
            CancellationToken cancellationToken = default);
}