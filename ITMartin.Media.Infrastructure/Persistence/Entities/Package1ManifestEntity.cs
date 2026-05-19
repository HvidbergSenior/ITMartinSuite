// File:
// ITMartin.Media.Infrastructure.Persistence.Entities/Package1ManifestEntity.cs

namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class Package1ManifestEntity
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public required string RootPath { get; set; }

    public int FileCount { get; set; }

    public required string MediaFilesJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}