namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IVideoEnhancementService
{
    Task<string> ApplyFiltersAsync(
        string inputPath,
        string videoFilterChain,
        string audioFilterChain,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default,
        int crf = 18,
        string preset = "slow",
        string codec = "libx264");

    Task<string> DeinterlaceAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);

    Task<string> StabilizeAsync(
        string inputPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);

    Task<string> DenoiseAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);

    Task<string> SharpenAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);

    Task<string> ColorCorrectAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);

    Task<string> UpscaleAsync(
        string inputPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);

    Task<string> CropAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default);
}