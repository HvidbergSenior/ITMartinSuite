using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class ImageProcessingService
    : IImageEnhancementService
{
    public async Task<string> ColorCorrectAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        return await CopyAsync(
            inputPath,
            "color");
    }

    public async Task<string> AdjustContrastAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        return await CopyAsync(
            inputPath,
            "contrast");
    }

    public async Task<string> DenoiseAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        return await CopyAsync(
            inputPath,
            "denoise");
    }

    public async Task<string> DeblurAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        return await CopyAsync(
            inputPath,
            "deblur");
    }

    public async Task<string> UpscaleAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        return await CopyAsync(
            inputPath,
            "upscale");
    }

    private static async Task<string> CopyAsync(
        string inputPath,
        string suffix)
    {
        var directory =
            Path.GetDirectoryName(inputPath)!;

        var fileName =
            Path.GetFileNameWithoutExtension(inputPath);

        var extension =
            Path.GetExtension(inputPath);

        var outputPath =
            Path.Combine(
                directory,
                $"{fileName}.{suffix}{extension}");

        File.Copy(
            inputPath,
            outputPath,
            overwrite: true);

        return await Task.FromResult(
            outputPath);
    }
}