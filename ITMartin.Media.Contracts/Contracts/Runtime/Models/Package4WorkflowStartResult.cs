namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed record Package4WorkflowStartResult(
    Guid WorkflowId,
    Package4WorkflowState State,
    bool HasRunThroughPackage1);
