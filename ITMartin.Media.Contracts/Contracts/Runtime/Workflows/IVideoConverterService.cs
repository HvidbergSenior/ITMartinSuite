namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IVideoConverterService
{
    Task<string> ConvertToUniversalMp4Async(
        string inputPath,
        string outputDirectory,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);
}