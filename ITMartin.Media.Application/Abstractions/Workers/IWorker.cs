namespace ITMartin.Media.Application.Abstractions.Workers;

public interface IWorker
{
    Task ExecuteJobAsync(
        CancellationToken cancellationToken);
}