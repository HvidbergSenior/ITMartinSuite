using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class FfmpegAudioExtractionService
    : FfmpegServiceBase,
        IAudioExtractionService
{
    public async Task<string> ExtractAsync(
        string videoPath,
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
            $"-y -i \"{videoPath}\" -vn -acodec pcm_s16le \"{outputPath}\"";

        await ExecuteAsync(
            arguments,
            cancellationToken);

        CopyDates(
            videoPath,
            outputPath);

        return outputPath;
    }

    public async Task<string> MuxAsync(
        string videoPath,
        string audioPath,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                videoPath,
                "muxed");

        var arguments =
            $"-y -i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -map 0:v:0 -map 1:a:0 \"{outputPath}\"";

        await ExecuteAsync(
            arguments,
            cancellationToken);

        CopyDates(
            videoPath,
            outputPath);

        return outputPath;
    }
}