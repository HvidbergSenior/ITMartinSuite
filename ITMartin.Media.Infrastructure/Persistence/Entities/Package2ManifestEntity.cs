namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class Package2ManifestEntity
{
    public Guid WorkflowId { get; set; }

    public required string PackageId { get; set; }

    public int FileCount { get; set; }

    public required string Profile { get; set; }

    public required string ItemsJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}