// File:
// ITMartin.Media.Application/Pipelines/Package1/Models/Package1Manifest.cs

using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Pipelines.Package1.Models;

public sealed class Package1Manifest
{
    public required Guid WorkflowId { get; init; }

    public required string RootPath { get; init; }

    public List<MediaFile>
        MediaFiles { get; init; } = [];

    public int FileCount { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}