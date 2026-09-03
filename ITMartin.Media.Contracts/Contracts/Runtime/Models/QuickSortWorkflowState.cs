namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class FailedFile
{
    public string FilePath { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public sealed class QuickSortWorkflowState
{
    public string RootPath { get; set; } = string.Empty;

    public List<MediaFile> MediaFiles { get; set; } = [];

    public List<DuplicateGroup> DuplicateGroups { get; set; } = [];

    public List<FailedFile> FailedFiles { get; set; } = [];

    public QuickSortCleanupResult? CleanupResult { get; set; }

    public QuickSortManifest? Manifest { get; set; }

    public QuickSortExportResult? ExportResult { get; set; }
    public int Version { get; set; } = 1;
    public string CurrentStep { get; set; } = string.Empty;
    public int? OverrideYear
    {
        get;
        init;
    }
    public bool EnableAiClassification { get; set; } = false;

    // Dead until wired up here - StartQuickSortRequest always carried this,
    // but QuickSortClient never copied it onto the state, so every workflow
    // step ran duplicate detection unconditionally regardless of what the
    // caller asked for.
    public bool EnableDeduplication { get; set; } = true;

    // QuickSortBaselineHelper's full-library mirror is cheap for a normal
    // per-client library, but for a large one-off re-sort (e.g. rebuilding a
    // ~150GB+ library from scratch) it silently doubles disk usage on every
    // run and can fill the drive (see 2026-08-24 incident: 144GB mirror on a
    // ~140GB library with no warning). Default true to preserve existing
    // behavior for normal-sized client libraries.
    public bool EnableBaselineSnapshot { get; set; } = true;

    public string? OutputPath { get; set; }
}