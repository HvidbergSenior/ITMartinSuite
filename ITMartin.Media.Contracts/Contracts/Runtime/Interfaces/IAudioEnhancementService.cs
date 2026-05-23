namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IAudioEnhancementService
{
    Task<string> ReduceNoiseAsync(
        string audioPath,
        CancellationToken cancellationToken = default);

    Task<string> RemoveHumAsync(
        string audioPath,
        CancellationToken cancellationToken = default);

    Task<string> NormalizeAudioAsync(
        string audioPath,
        CancellationToken cancellationToken = default);

    Task<string> EnhanceSpeechAsync(
        string audioPath,
        CancellationToken cancellationToken = default);
}