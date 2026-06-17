using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class FfmpegVideoProcessingService
    : FfmpegServiceBase,
      IVideoEnhancementService
{
    public async Task<string> ApplyFiltersAsync(
        string inputPath,
        string videoFilterChain,
        string audioFilterChain,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default,
        int crf = 18,
        string preset = "slow",
        string codec = "libx264")
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "restored");

        var arguments =
            BuildArguments(
                inputPath,
                outputPath,
                videoFilterChain,
                audioFilterChain,
                codec,
                crf,
                preset);

        await ExecuteAsync(
            arguments,
            onProgress,
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    private static string BuildArguments(
        string inputPath,
        string outputPath,
        string videoFilterChain,
        string audioFilterChain,
        string codec,
        int crf,
        string preset)
    {
        var arguments =
            $"-hide_banner -y -i \"{inputPath}\" ";

        if (!string.IsNullOrWhiteSpace(
                videoFilterChain))
        {
            arguments +=
                $"-vf \"{videoFilterChain}\" ";
        }

        if (!string.IsNullOrWhiteSpace(
                audioFilterChain))
        {
            arguments +=
                $"-af \"{audioFilterChain}\" ";
        }

        arguments +=
            $"-c:v {codec} " +
            $"-crf {crf} " +
            $"-preset {preset} " +
            "-profile:v high " +
            "-level:v 4.1 " +
            "-pix_fmt yuv420p " +
            "-movflags +faststart " +
            "-c:a aac " +
            "-b:a 192k " +
            $"\"{outputPath}\"";

        return arguments;
    }

    public async Task<string> DeinterlaceAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFiltersAsync(
            inputPath,
            filter,
            string.Empty,
            onProgress,
            cancellationToken);
    }

    public async Task<string> StabilizeAsync(
        string inputPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFiltersAsync(
            inputPath,
            "vidstabtransform=smoothing=10",
            string.Empty,
            onProgress,
            cancellationToken);
    }

    public async Task<string> DenoiseAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFiltersAsync(
            inputPath,
            filter,
            string.Empty,
            onProgress,
            cancellationToken);
    }

    public async Task<string> SharpenAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFiltersAsync(
            inputPath,
            filter,
            string.Empty,
            onProgress,
            cancellationToken);
    }

    public async Task<string> ColorCorrectAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFiltersAsync(
            inputPath,
            filter,
            string.Empty,
            onProgress,
            cancellationToken);
    }

    public async Task<string> UpscaleAsync(
        string inputPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFiltersAsync(
            inputPath,
            "scale=-2:1080:flags=lanczos",
            string.Empty,
            onProgress,
            cancellationToken);
    }

    public async Task<string> CropAsync(
        string inputPath,
        string filter,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFiltersAsync(
            inputPath,
            filter,
            string.Empty,
            onProgress,
            cancellationToken);
    }
}
