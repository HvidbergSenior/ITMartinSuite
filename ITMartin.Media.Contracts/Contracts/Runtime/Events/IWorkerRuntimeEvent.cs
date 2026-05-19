// File: ITMartin.Media.Contracts/Runtime/IWorkerRuntimeEvent.cs

namespace ITMartin.Media.Infrastructure.Contracts.Runtime;

public interface IWorkerRuntimeEvent
{
    Guid EventId { get; }
    DateTimeOffset TimestampUtc { get; }
    string NodeId { get; }
}