namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed record WorkflowCheckpoint(
    Guid WorkflowId,
    string WorkflowName,
    string StepName,
    string StateJson,
    DateTimeOffset CreatedAt);