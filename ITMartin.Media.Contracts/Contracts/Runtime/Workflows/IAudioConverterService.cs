namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IAudioConverterService
{
    Task<string> ConvertToMp3Async(
        string inputPath,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
