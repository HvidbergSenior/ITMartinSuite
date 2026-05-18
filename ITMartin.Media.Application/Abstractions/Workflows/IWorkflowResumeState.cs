namespace ITMartin.Media.Application.Abstractions.Workflows;

public sealed class IWorkflowResumeState
{
    public Guid WorkflowId { get; init; }

    public string WorkflowName { get; init; } = string.Empty;

    public string? LastCompletedStep { get; init; }
}