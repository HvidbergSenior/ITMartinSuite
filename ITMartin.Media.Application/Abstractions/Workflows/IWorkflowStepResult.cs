// File: ITMartin.Media.Application/Workflows/WorkflowStepResult.cs

namespace ITMartin.Media.Application.Abstractions.Workflows;

public sealed class WorkflowStepResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public static WorkflowStepResult Completed()
    {
        return new WorkflowStepResult
        {
            Success = true
        };
    }

    public static WorkflowStepResult Failed(string error)
    {
        return new WorkflowStepResult
        {
            Success = false,
            Error = error
        };
    }
}