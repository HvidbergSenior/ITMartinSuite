using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IVideoEnhancementService
{
    Task<string> DeinterlaceAsync(
        string videoPath,
        CancellationToken cancellationToken = default);

    Task<string> StabilizeAsync(
        string videoPath,
        CancellationToken cancellationToken = default);

    Task<string> DenoiseAsync(
        string videoPath,
        CancellationToken cancellationToken = default);

    Task<string> SharpenAsync(
        string videoPath,
        CancellationToken cancellationToken = default);

    Task<string> ColorCorrectAsync(
        string videoPath,
        CancellationToken cancellationToken = default);

    Task<string> UpscaleAsync(
        string videoPath,
        CancellationToken cancellationToken = default);
}