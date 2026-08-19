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
