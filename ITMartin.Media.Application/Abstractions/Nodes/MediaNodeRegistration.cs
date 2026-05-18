// File: ITMartin.Media.Application/Nodes/MediaNodeRegistration.cs

namespace ITMartin.Media.Application.Abstractions.Nodes;

public sealed class MediaNodeRegistration
{
    public required string NodeId { get; init; }

    public required string HostName { get; init; }

    public required string Version { get; init; }

    public required IReadOnlyCollection<string> Capabilities { get; init; }

    public required IReadOnlyCollection<string> Queues { get; init; }

    public DateTimeOffset LastHeartbeatUtc { get; set; }

    public int WorkerCount { get; init; }
}