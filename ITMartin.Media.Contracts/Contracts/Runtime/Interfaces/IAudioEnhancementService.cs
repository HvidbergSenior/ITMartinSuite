namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IAudioEnhancementService
{
    Task<string> ReduceNoiseAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> RemoveHumAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> NormalizeAudioAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> EnhanceSpeechAsync(
        string inputPath,
        CancellationToken cancellationToken = default);
}