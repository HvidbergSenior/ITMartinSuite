namespace ITMartin.Media.Infrastructure.Persistence.Entities;

/// <summary>
/// One row per (library path, index type) that has ever been indexed for
/// Package3. Faces and Scenes are tracked separately since Faces is free/local
/// and Scenes has a real per-file API cost - they run independently.
/// Polled by the UI for progress.
/// </summary>
public sealed class Package3IndexStatusEntity
{
    public Guid Id { get; set; }

    public required string LibraryPath { get; set; }

    /// <summary>Faces | Scenes</summary>
    public required string IndexType { get; set; }

    /// <summary>Running | Completed | Failed</summary>
    public required string Status { get; set; }

    public int TotalFiles { get; set; }

    public int ProcessedFiles { get; set; }

    public string? CurrentFile { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
