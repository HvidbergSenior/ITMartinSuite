namespace ITMartin.Media.Domain.Entities;

public sealed class ScanSession
{
    public Guid Id { get; set; }

    public string RootPath { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public long FilesDiscovered { get; set; }

    public long FilesProcessed { get; set; }

    public long FilesFailed { get; set; }

    public string SnapshotPath { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}