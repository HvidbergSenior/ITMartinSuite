namespace ITMartin.Media.Application.Pipelines.Package1.Models;

public interface IPackage1ManifestStore
{
    Task SaveAsync(
        Package1Manifest manifest,
        CancellationToken cancellationToken = default);

    Task<Package1Manifest?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}