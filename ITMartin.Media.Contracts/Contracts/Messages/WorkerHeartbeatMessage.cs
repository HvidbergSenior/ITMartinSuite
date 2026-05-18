// File: ITMartin.Media.Contracts/Messages/WorkerHeartbeatMessage.cs

namespace ITMartin.Media.Infrastructure.Contracts.Messages;

public sealed record WorkerHeartbeatMessage(
    string NodeId,
    string WorkerName,
    string Status,
    int ActiveWorkflows,
    DateTimeOffset TimestampUtc);