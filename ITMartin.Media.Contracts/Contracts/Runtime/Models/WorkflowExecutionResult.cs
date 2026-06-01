namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class WorkflowStepExecutionResult
{
    public required string StepName { get; init; }

    public bool Success { get; init; }

    public string? Message { get; init; }

    public TimeSpan Duration { get; init; }
}