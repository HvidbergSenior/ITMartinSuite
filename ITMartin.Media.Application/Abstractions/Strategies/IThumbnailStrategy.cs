namespace ITMartin.Media.Application.Abstractions.Strategies;

public interface IThumbnailStrategy
{
    bool Supports(string mimeType);

    Task<string> GenerateAsync(
        string filePath,
        CancellationToken cancellationToken);
}