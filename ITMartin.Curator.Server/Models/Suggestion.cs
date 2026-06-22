namespace ITMartin.Curator.Server.Models;

public enum SuggestionType
{
    RenameGeneric,
    BurstShots,
    DuplicateFiles,
    GroupByDate,
    GroupByLocation,
    GroupByPerson,
}

public sealed class Suggestion
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public SuggestionType Type { get; init; }
    public string Icon { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public List<string> AffectedFiles { get; init; } = [];
    public bool IsDismissed { get; set; }
    public bool IsExpanded { get; set; }

    public List<RenamePreviewItem> RenamePreview { get; init; } = [];
    public List<BurstGroup> BurstGroups { get; init; } = [];
    public List<DuplicateGroup> DuplicateGroups { get; init; } = [];
}

public sealed record RenamePreviewItem(string OldPath, string NewName)
{
    public string OldName => Path.GetFileName(OldPath);
    public string Folder  => Path.GetDirectoryName(OldPath) ?? "";
}

public sealed class BurstGroup
{
    public List<string> Files { get; init; } = [];
    public DateTime Timestamp { get; init; }
    public string? KeptFile { get; set; }
}

public sealed class DuplicateGroup
{
    public string FileName { get; init; } = "";
    public List<string> Paths { get; init; } = [];
}
