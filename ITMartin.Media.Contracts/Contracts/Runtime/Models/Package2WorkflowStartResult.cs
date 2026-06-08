namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed record Package2WorkflowStartResult(
    Guid WorkflowId,
    Package2WorkflowState State);