using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

namespace ITMartin.Media.Application.Abstractions.BackgroundJobs;

public interface IBackgroundJobHandler
{
    string JobType { get; }

    Task HandleAsync(
        BackgroundJob job,
        CancellationToken cancellationToken);
}