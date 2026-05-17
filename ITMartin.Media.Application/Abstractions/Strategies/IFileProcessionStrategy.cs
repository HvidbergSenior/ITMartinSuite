namespace ITMartin.Media.Application.Abstractions.Strategies;

public interface IFileProcessingStrategy
{
    bool CanHandle(string extension);

    Task ProcessAsync(
        string path,
        CancellationToken cancellationToken);
}