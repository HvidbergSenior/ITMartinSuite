namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public sealed record StartWorkflowMessage(
    Guid WorkflowId,
    string WorkflowName,
    string FilePath,
    DateTimeOffset CreatedAt);