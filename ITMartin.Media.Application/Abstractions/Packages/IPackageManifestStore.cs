
using ITMartin.Media.Application.Pipelines.Package1.Models;

namespace ITMartin.Media.Application.Abstractions.Packages;

public interface IPackageManifestStore
{
    Task SaveAsync(
        Package1Manifest manifest,
        CancellationToken cancellationToken = default);

    Task<Package1Manifest?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}