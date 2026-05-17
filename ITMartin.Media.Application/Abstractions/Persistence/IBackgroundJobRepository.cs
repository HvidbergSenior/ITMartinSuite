using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

namespace ITMartin.Media.Application.Abstractions.Persistence;

public interface IBackgroundJobRepository
{
    Task CreateAsync(
        BackgroundJob job,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        BackgroundJob job,
        CancellationToken cancellationToken);

    Task<BackgroundJob?> GetAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BackgroundJob>> GetPendingAsync(
        string queue,
        CancellationToken cancellationToken);
}