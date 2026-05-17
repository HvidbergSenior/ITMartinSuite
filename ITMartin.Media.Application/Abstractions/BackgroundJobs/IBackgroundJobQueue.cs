using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

namespace ITMartin.Media.Application.Abstractions.BackgroundJobs;

public interface IBackgroundJobQueue
{
    Task EnqueueAsync(
        BackgroundJob job,
        CancellationToken cancellationToken);

    Task<BackgroundJob?> DequeueAsync(
        string queue,
        CancellationToken cancellationToken);
}