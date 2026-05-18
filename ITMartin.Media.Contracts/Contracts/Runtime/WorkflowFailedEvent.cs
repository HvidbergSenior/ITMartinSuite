// File: ITMartin.Media.Contracts/Runtime/WorkflowFailedEvent.cs

namespace ITMartin.Media.Infrastructure.Contracts.Runtime;

public sealed record WorkflowFailedEvent(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    string NodeId,
    Guid WorkflowId,
    string WorkflowName,
    string Error,
    string? StackTrace,
    string CorrelationId)
    : IWorkerRuntimeEvent;