using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class FfmpegVideoProcessingService
    : FfmpegServiceBase,
      IVideoEnhancementService
{
    public async Task<string> DeinterlaceAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "deinterlaced");

        await ExecuteAsync(
            $"-hide_banner -y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"",
            onProgress,
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> StabilizeAsync(
        string inputPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "stabilized");

        await ExecuteAsync(
            $"-hide_banner -y -i \"{inputPath}\" " +
            "-vf vidstabtransform=smoothing=30 " +
            $"\"{outputPath}\"",
            onProgress,
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> DenoiseAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "denoised");

        await ExecuteAsync(
            $"-hide_banner -y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"",
            onProgress,
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> SharpenAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "sharpened");

        await ExecuteAsync(
            $"-hide_banner -y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"",
            onProgress,
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> ColorCorrectAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "color");

        await ExecuteAsync(
            $"-hide_banner -y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"",
            onProgress,
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> UpscaleAsync(
        string inputPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "upscaled");

        await ExecuteAsync(
            $"-hide_banner -y -i \"{inputPath}\" -vf scale=iw*2:ih*2 \"{outputPath}\"",
            onProgress,
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> CropAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "cropped");

        await ExecuteAsync(
            $"-hide_banner -y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"",
            onProgress,
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }
}