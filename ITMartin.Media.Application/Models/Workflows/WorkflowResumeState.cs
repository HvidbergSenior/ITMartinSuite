namespace ITMartin.Media.Application.Models.Workflows;

public sealed record WorkflowResumeState(
    Guid WorkflowId,
    string? LastCompletedStep,
    DateTimeOffset UpdatedAt);