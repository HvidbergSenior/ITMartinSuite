namespace ITMartin.Media.Application.Abstractions.Distributed;

using ITMartin.Media.Application.Models.Distributed;

public interface IWorkerNodeRegistry
{
    Task RegisterAsync(
        WorkerNode node,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkerNode>>
        GetAllAsync(
            CancellationToken cancellationToken);
}