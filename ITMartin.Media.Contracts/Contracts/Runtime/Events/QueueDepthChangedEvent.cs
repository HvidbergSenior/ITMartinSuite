// File: ITMartin.Media.Contracts/Runtime/QueueDepthChangedEvent.cs

namespace ITMartin.Media.Infrastructure.Contracts.Runtime;

public sealed record QueueDepthChangedEvent(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    string NodeId,
    string QueueName,
    int Depth)
    : IWorkerRuntimeEvent;