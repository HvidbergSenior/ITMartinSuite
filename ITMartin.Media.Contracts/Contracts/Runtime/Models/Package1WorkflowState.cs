namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class Package1WorkflowState
{
    public string RootPath { get; set; } = string.Empty;

    public List<MediaFile> MediaFiles { get; set; } = [];

    public List<DuplicateGroup> DuplicateGroups { get; set; } = [];

    public Package1CleanupResult? CleanupResult { get; set; }

    public Package1Manifest? Manifest { get; set; }

    public Package1ExportResult? ExportResult { get; set; }
    public int Version { get; set; } = 1;
    public string CurrentStep { get; set; } = string.Empty;
    public int? OverrideYear
    {
        get;
        init;
    }
    public bool EnableSegmentation { get; set; } = false;

    public bool EnableAiClassification { get; set; } = false;

    public string? OutputPath { get; set; }
}