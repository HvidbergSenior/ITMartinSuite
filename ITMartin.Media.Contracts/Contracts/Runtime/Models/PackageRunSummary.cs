namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public enum ReconciliationVerdict
{
    Verified,   // source count == exported + duplicates + failed, nothing unaccounted for
    Mismatch,   // the numbers don't add up - something is unaccounted for
    Unknown,    // not enough data was saved to check (older/incomplete runs)
    InProgress  // still running - the numbers are necessarily incomplete, not a real verdict yet
}

public sealed class PackageRunSummary
{
    public required Guid WorkflowId { get; init; }
    public required string WorkflowName { get; init; }
    public required string Status { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }

    public string? SourcePath { get; init; }
    public string? OutputPath { get; init; }

    public int SourceFileCount { get; init; }
    public int ExportedFileCount { get; init; }
    public int DuplicateFileCount { get; init; }
    public int FailedFileCount { get; init; }

    public long SourceSizeBytes { get; init; }
    public long ExportedSizeBytes { get; init; }

    public List<(string FilePath, string Reason)> FailedFiles { get; init; } = [];

    public ReconciliationVerdict Verdict { get; init; }
}
