namespace ITMartin.Media.Application.Pipelines.Package1.Models;

public sealed class Package1Manifest
{
    public Guid WorkflowId { get; set; }

    public required string RootPath { get; set; }

    public int FileCount { get; set; }

    public List<string> Files { get; set; } = [];

    public List<string> HashedFiles { get; set; } = [];

    public List<string> MetadataFiles { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
}