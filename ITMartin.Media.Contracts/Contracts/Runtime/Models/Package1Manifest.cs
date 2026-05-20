namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class Package1Manifest
{
    public required Guid WorkflowId { get; init; }

    public required string RootPath { get; init; }

    public List<MediaFile>
        MediaFiles { get; init; } = [];

    public int FileCount { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}