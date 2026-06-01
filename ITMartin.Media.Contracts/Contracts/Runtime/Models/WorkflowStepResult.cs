namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public class WorkflowStepResult
{
    public required string Name { get; init; }

    public bool Success { get; init; }

    public string? Message { get; init; }

    public TimeSpan Duration { get; init; }
}