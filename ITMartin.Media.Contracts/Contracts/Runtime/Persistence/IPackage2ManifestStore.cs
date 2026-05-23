using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

public interface IPackage2ManifestStore
{
    Task SaveAsync(
        Package2Manifest manifest,
        CancellationToken cancellationToken = default);

    Task<Package2Manifest?> GetAsync(
        Guid packageId,
        CancellationToken cancellationToken = default);
}