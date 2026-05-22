using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

public interface IPackage1ManifestStore
{
    Task SaveAsync(
        Package1Manifest manifest,
        CancellationToken cancellationToken = default);

    Task<Package1Manifest?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}