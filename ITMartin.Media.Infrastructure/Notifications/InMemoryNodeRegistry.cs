// File: ITMartin.Media.Infrastructure/Nodes/InMemoryNodeRegistry.cs

using ITMartin.Media.Application.Abstractions.Nodes;

namespace ITMartin.Media.Infrastructure.Notifications;

public sealed class InMemoryNodeRegistry
    : INodeRegistry
{
    private readonly Dictionary<string, MediaNodeRegistration> _nodes = [];

    public Task RegisterAsync(
        MediaNodeRegistration registration,
        CancellationToken cancellationToken = default)
    {
        registration.LastHeartbeatUtc = DateTimeOffset.UtcNow;

        _nodes[registration.NodeId] = registration;

        return Task.CompletedTask;
    }

    public Task HeartbeatAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.LastHeartbeatUtc = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<MediaNodeRegistration>> GetNodesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<MediaNodeRegistration>>(
            _nodes.Values.ToList());
    }
}