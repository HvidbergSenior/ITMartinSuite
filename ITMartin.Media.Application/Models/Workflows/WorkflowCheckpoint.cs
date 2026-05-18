// File: ITMartin.Media.Application/Models/Workflows/WorkflowCheckpoint.cs

namespace ITMartin.Media.Application.Models.Workflows;

public sealed record WorkflowCheckpoint(
    Guid WorkflowId,
    string WorkflowName,
    string StepName,
    string StateJson,
    DateTimeOffset CreatedAt);