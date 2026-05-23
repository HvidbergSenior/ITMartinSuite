namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IImageEnhancementService
{
    Task<string> ColorCorrectAsync(
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<string> AdjustContrastAsync(
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<string> DenoiseAsync(
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<string> DeblurAsync(
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<string> UpscaleAsync(
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<string> CorrectAspectRatioAsync(
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<string> RemoveBordersAsync(
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<string> AutoCropAsync(
        string imagePath,
        CancellationToken cancellationToken = default);
}