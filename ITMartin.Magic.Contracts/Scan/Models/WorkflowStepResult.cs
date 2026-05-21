namespace ITMartin.Magic.Contracts.Scan.Models;

public sealed record WorkflowStepResult(
    string StepName,
    bool Success,
    double Confidence,
    TimeSpan Duration,
    string? Error);