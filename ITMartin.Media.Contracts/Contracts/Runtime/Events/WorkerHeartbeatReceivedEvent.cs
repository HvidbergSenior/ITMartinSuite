// File: ITMartin.Media.Contracts/Runtime/WorkerHeartbeatReceivedEvent.cs

namespace ITMartin.Media.Infrastructure.Contracts.Runtime;

public sealed record WorkerHeartbeatReceivedEvent(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    string NodeId,
    string WorkerName,
    string Status,
    int ActiveWorkflows)
    : IWorkerRuntimeEvent;