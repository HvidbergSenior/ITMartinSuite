namespace ITMartin.Media.Application.Pipelines.Package1.Models;

public sealed class Package1ManifestBuilder
{
    public Package1Manifest Build(
        Guid workflowId,
        Package1WorkflowState state)
    {
        return new Package1Manifest
        {
            WorkflowId = workflowId,
            RootPath = state.RootPath,
            FileCount = state.Files.Count,
            Files = state.Files.ToList(),
            HashedFiles = state.HashedFiles.ToList(),
            MetadataFiles = state.MetadataFiles.ToList(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}