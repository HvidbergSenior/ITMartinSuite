namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class Package1ManifestEntity
{
    public Guid WorkflowId { get; set; }

    public required string RootPath { get; set; }

    public int FileCount { get; set; }

    public required string FilesJson { get; set; }

    public required string HashedFilesJson { get; set; }

    public required string MetadataFilesJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}