// File: ITMartin.Media.Application/Abstractions/Nodes/INodeRegistry.cs

namespace ITMartin.Media.Application.Abstractions.Nodes;

public interface INodeRegistry
{
    Task RegisterAsync(
        MediaNodeRegistration registration,
        CancellationToken cancellationToken = default);

    Task HeartbeatAsync(
        string nodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MediaNodeRegistration>> GetNodesAsync(
        CancellationToken cancellationToken = default);
}