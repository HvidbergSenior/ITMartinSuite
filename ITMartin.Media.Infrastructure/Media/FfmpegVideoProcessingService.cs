using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class FfmpegVideoProcessingService
    : FfmpegServiceBase,
      IVideoEnhancementService
{
    public async Task<string> DeinterlaceAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "deinterlaced");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -vf yadif \"{outputPath}\"",
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
        var transformPath =
            BuildOutputPath(
                inputPath,
                "transforms");

        var outputPath =
            BuildOutputPath(
                inputPath,
                "stabilized");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" " +
            "-vf vidstabdetect=shakiness=5:accuracy=15 " +
            "-f null -",
            cancellationToken);

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" " +
            $"-vf vidstabtransform=smoothing=30 " +
            $"\"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> DenoiseAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "denoised");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -vf hqdn3d \"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> SharpenAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "sharpened");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -vf unsharp=5:5:1.0:5:5:0.0 \"{outputPath}\"",
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> ColorCorrectAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "color");

        await ExecuteAsync(
            $"-y -i \"{inputPath}\" -vf eq=contrast=1.05:saturation=1.1 \"{outputPath}\"",
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
}