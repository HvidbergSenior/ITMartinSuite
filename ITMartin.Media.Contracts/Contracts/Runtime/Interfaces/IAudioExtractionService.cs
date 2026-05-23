namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IAudioExtractionService
{
    Task<string> ExtractAsync(
        string videoPath,
        CancellationToken cancellationToken = default);

    Task<string> MuxAsync(
        string videoPath,
        string audioPath,
        CancellationToken cancellationToken = default);
}