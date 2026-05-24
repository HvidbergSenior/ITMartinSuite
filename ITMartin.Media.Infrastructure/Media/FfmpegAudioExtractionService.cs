using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class FfmpegAudioExtractionService
    : FfmpegServiceBase,
        IAudioExtractionService
{
    public async Task<string> ExtractAsync(
        string videoPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                videoPath,
                "audio");

        outputPath =
            Path.ChangeExtension(
                outputPath,
                ".wav");

        var arguments =
            $"-hide_banner -y -i \"{videoPath}\" " +
            $"-vn -acodec pcm_s16le \"{outputPath}\"";

        await ExecuteAsync(
            arguments,
            onProgress,
            cancellationToken);

        CopyDates(
            videoPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> MuxAsync(
        string videoPath,
        string audioPath,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                videoPath,
                "muxed");

        var arguments =
            $"-hide_banner -y " +
            $"-i \"{videoPath}\" " +
            $"-i \"{audioPath}\" " +
            "-c:v copy " +
            "-map 0:v:0 " +
            "-map 1:a:0 " +
            $"\"{outputPath}\"";

        await ExecuteAsync(
            arguments,
            onProgress,
            cancellationToken);

        CopyDates(
            videoPath,
            outputPath);

        return outputPath;
    }
}