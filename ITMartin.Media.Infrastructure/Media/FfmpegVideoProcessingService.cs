using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class FfmpegVideoProcessingService
    : FfmpegServiceBase,
      IVideoEnhancementService
{
    public async Task<string> DeinterlaceAsync(
        string inputPath,
        string filter,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "deinterlaced");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> StabilizeAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "stabilized");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" " +
            "-vf vidstabtransform=smoothing=30 " +
            $"\"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> DenoiseAsync(
        string inputPath,
        string filter,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "denoised");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> SharpenAsync(
        string inputPath,
        string filter,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "sharpened");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> ColorCorrectAsync(
        string inputPath,
        string filter,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "color");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> UpscaleAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "upscaled");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -vf scale=iw*2:ih*2 \"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }
    public async Task<string> CropAsync(
        string inputPath,
        string filter,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "cropped");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }
}