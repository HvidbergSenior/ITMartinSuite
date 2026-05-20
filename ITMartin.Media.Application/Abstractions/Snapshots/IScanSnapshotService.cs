namespace ITMartin.Media.Application.Abstractions.Snapshots;

public interface IScanSnapshotService
{
    Task SaveAsync(
        Guid sessionId,
        object state,
        CancellationToken cancellationToken);

    Task<T?> LoadAsync<T>(
        Guid sessionId,
        CancellationToken cancellationToken);
}