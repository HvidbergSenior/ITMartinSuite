namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IVideoEnhancementService
{
    Task<string> DeinterlaceAsync(
        string inputPath,
        string filter,
        CancellationToken cancellationToken = default);

    Task<string> StabilizeAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    Task<string> DenoiseAsync(
        string inputPath,
        string filter,
        CancellationToken cancellationToken = default);

    Task<string> SharpenAsync(
        string inputPath,
        string filter,
        CancellationToken cancellationToken = default);

    Task<string> ColorCorrectAsync(
        string inputPath,
        string filter,
        CancellationToken cancellationToken = default);

    Task<string> UpscaleAsync(
        string inputPath,
        CancellationToken cancellationToken = default);
    Task<string> CropAsync(
        string inputPath,
        string filter,
        CancellationToken cancellationToken = default);
}