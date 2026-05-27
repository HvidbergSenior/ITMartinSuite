using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class FfmpegVideoSampleService
    : FfmpegServiceBase,
        IVideoSampleService
{
    public async Task<string> CreateSampleAsync(
        string inputPath,
        TimeSpan start,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var outputPath =
            BuildOutputPath(
                inputPath,
                $"sample_{start.TotalSeconds:0}");

        var arguments =
            BuildArguments(
                inputPath,
                outputPath,
                start,
                duration);

        await ExecuteAsync(
            arguments,
            null,
            cancellationToken);

        CopyDates(
            inputPath,
            outputPath);

        return outputPath;
    }

    private static string BuildArguments(
        string inputPath,
        string outputPath,
        TimeSpan start,
        TimeSpan duration)
    {
        return
            $"-hide_banner -y " +
            $"-ss {start:hh\\:mm\\:ss} " +
            $"-i \"{inputPath}\" " +
            $"-t {duration:hh\\:mm\\:ss} " +
            "-c copy " +
            $"\"{outputPath}\"";
    }
}