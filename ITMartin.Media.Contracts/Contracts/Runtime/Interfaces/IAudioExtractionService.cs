namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IAudioExtractionService
{
    Task<string> ExtractAsync(
        string videoPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);

    Task<string> MuxAsync(
        string videoPath,
        string audioPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);
}