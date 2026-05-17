namespace ITMartin.Media.Application.Abstractions.Distributed;

public interface IDistributedCoordinator
{
    Task<bool> TryAcquireAsync(
        string resource,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        string resource,
        CancellationToken cancellationToken = default);
}