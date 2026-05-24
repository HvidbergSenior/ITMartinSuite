using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IAudioEnhancementService
{
    Task<string> ReduceNoiseAsync(
        string inputPath,
        RestorationProfile profile,
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