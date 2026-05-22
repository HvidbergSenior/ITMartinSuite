namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IVideoEnhancementService
{
    Task<string> DeinterlaceAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> StabilizeAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> DenoiseAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> SharpenAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> ColorCorrectAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> UpscaleAsync(
        string inputPath,
        CancellationToken cancellationToken = default);
}