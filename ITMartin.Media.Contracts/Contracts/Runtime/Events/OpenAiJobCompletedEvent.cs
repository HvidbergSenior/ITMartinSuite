// File: ITMartin.Media.Contracts/Runtime/OpenAiJobCompletedEvent.cs

namespace ITMartin.Media.Infrastructure.Contracts.Runtime;

public sealed record OpenAiJobCompletedEvent(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    string NodeId,
    Guid MediaId,
    string Model,
    TimeSpan Duration)
    : IWorkerRuntimeEvent;