namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IImageEnhancementService
{
    Task<string> ColorCorrectAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> AdjustContrastAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> DenoiseAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> DeblurAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> UpscaleAsync(
        string inputPath,
        CancellationToken cancellationToken = default);
}