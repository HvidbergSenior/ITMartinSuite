using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

public interface IQuickSortManifestStore
{
    Task SaveAsync(
        QuickSortManifest manifest,
        CancellationToken cancellationToken = default);

    Task<QuickSortManifest?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}