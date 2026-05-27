using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

namespace ITMartin.Media.Application.Abstractions.BackgroundJobs;

public interface IBackgroundJobQueue
{
    Task EnqueueAsync(
        BackgroundJob job,
        CancellationToken cancellationToken);

    void Subscribe(
        string queue,
        Func<BackgroundJob, Task> handler);
}