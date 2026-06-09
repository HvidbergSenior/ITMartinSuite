namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IThumbnailService
{
    Task<string> GenerateAsync(
        string sourcePath,
        string outputPath,
        CancellationToken cancellationToken = default);
    bool Supports(string path);
}