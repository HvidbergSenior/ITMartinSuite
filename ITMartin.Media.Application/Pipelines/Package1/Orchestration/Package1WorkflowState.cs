namespace ITMartin.Media.Application.Pipelines.Package1.Orchestration;

public sealed class Package1WorkflowState
{
    public required string RootPath { get; init; }

    public List<string> Files { get; set; } = [];

    public List<string> HashedFiles { get; set; } = [];

    public List<string> MetadataFiles { get; set; } = [];
}