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
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                "restored");

        var encoder =
            OperatingSystem.IsWindows()
                ? "h264_qsv"
                : "libx264";

        var arguments =
            BuildArguments(
                inputPath,
                outputPath,
                videoFilterChain,
                audioFilterChain,
                encoder);

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
        string encoder)
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
            $"-c:v {encoder} " +
            "-preset veryfast " +
            "-pix_fmt yuv420p " +
            "-movflags +faststart " +
            "-c:a aac " +
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
            "scale=-2:1080",
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