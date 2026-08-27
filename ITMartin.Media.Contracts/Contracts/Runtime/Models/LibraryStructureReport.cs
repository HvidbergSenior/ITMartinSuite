namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class StructureIssue
{
    public string Context { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class LibraryStructureReport
{
    public List<string> ExpectedFoldersFound { get; init; } = [];
    public List<string> ExpectedFoldersMissing { get; init; } = [];
    public int CollectionsChecked { get; init; }
    public int PathsChecked { get; init; }
    public List<StructureIssue> Issues { get; init; } = [];
    public DateTime CheckedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class StructureRepairResult
{
    public bool CollectionsFileFound { get; init; }
    public int NormalizedPaths { get; init; }
    public int RecoveredAbsolutePaths { get; init; }
    public int RemovedMissingPaths { get; init; }
}

public sealed class DeliveryStructureIssue
{
    public string RelativePath { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

// Automated check for "does this delivered library actually look like it
// should" - extensions present per category, and Year/Month folder shape
// matches the current threshold rules (see LibraryExportService's
// MonthSplitThreshold/MonthHalfSplitThreshold). Metadata-only, no file
// content read - safe to run directly against a NAS mount or external HD.
public sealed class DeliveryStructureReport
{
    public int YearFoldersChecked { get; init; }
    public Dictionary<string, List<string>> ExtensionsByCategory { get; init; } = [];
    public List<DeliveryStructureIssue> Issues { get; init; } = [];
    public DateTime CheckedAtUtc { get; init; } = DateTime.UtcNow;
}
