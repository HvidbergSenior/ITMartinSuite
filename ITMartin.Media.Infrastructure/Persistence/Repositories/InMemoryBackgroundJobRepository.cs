using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

namespace ITMartin.Media.Infrastructure.Persistence.Repositories;

using ITMartin.Media.Application.Abstractions.Persistence;

public sealed class InMemoryBackgroundJobRepository
    : IBackgroundJobRepository
{
    private static readonly List<BackgroundJob>
        Jobs = [];

    public Task CreateAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        Jobs.Add(job);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<BackgroundJob?> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var job =
            Jobs.FirstOrDefault(
                x => x.Id == id);

        return Task.FromResult(job);
    }

    public Task<IReadOnlyCollection<BackgroundJob>>
        GetPendingAsync(
            string queue,
            CancellationToken cancellationToken)
    {
        IReadOnlyCollection<BackgroundJob> jobs =
            Jobs
                .Where(x =>
                    x.Queue == queue &&
                    x.Status == "Pending")
                .ToList();

        return Task.FromResult(jobs);
    }
}