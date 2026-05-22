namespace ITMartin.Media.Contracts.Contracts.Runtime.BackgroundJobs;

public sealed class WorkflowPipelineRequest
{
    public Guid WorkflowId { get; init; }

    public required string PackageName { get; init; }

    public required string SourcePath { get; init; }
}