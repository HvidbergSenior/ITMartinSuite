// File: ITMartin.Media.Contracts/Runtime/WorkflowCompletedEvent.cs

namespace ITMartin.Media.Infrastructure.Contracts.Runtime;

public sealed record WorkflowCompletedEvent(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    string NodeId,
    Guid WorkflowId,
    string WorkflowName,
    TimeSpan Duration,
    string CorrelationId)
    : IWorkerRuntimeEvent;