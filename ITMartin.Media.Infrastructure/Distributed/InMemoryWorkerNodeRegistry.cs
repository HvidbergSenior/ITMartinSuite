namespace ITMartin.Media.Infrastructure.Distributed;

using ITMartin.Media.Application.Abstractions.Distributed;
using ITMartin.Media.Application.Models.Distributed;

public sealed class InMemoryWorkerNodeRegistry
    : IWorkerNodeRegistry
{
    private static readonly List<WorkerNode>
        Nodes = [];

    public Task RegisterAsync(
        WorkerNode node,
        CancellationToken cancellationToken)
    {
        Nodes.RemoveAll(x =>
            x.Id == node.Id);

        Nodes.Add(node);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<WorkerNode>>
        GetAllAsync(
            CancellationToken cancellationToken)
    {
        IReadOnlyCollection<WorkerNode>
            results =
                Nodes.ToList();

        return Task.FromResult(results);
    }
}