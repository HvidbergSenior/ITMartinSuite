// File: ITMartin.Media.Contracts/Messages/WorkflowExecutionMessage.cs

namespace ITMartin.Media.Infrastructure.Contracts.Messages;

public sealed record WorkflowExecutionMessage(
    Guid WorkflowId,
    string WorkflowName,
    string CorrelationId,
    Dictionary<string, string> Input);