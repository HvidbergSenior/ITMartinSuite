namespace ITMartin.Media.Application.Abstractions.Locking;

public interface IDistributedLockService
{
    Task<bool> AcquireAsync(
        string key,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        string key,
        CancellationToken cancellationToken);
}