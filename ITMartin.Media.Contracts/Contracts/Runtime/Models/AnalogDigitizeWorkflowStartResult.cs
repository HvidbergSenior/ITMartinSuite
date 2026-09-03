namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed record AnalogDigitizeWorkflowStartResult(
    Guid WorkflowId,
    AnalogDigitizeWorkflowState State);