// File: ITMartin.Media.Contracts/Runtime/WorkflowStartedEvent.cs

namespace ITMartin.Media.Infrastructure.Contracts.Runtime;

public sealed record WorkflowStartedEvent(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    string NodeId,
    Guid WorkflowId,
    string WorkflowName,
    string CorrelationId)
    : IWorkerRuntimeEvent;