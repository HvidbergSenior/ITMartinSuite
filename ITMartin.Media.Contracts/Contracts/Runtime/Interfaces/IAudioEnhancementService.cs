using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IAudioEnhancementService
{
    Task<string> ReduceNoiseAsync(
        string inputPath,
        RestorationProfile profile,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);

    Task<string> RemoveHumAsync(
        string inputPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);

    Task<string> NormalizeAudioAsync(
        string inputPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);

    Task<string> EnhanceSpeechAsync(
        string inputPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);
}