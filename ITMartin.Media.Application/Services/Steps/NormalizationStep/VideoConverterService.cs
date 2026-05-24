using System.Diagnostics;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

public class VideoConverterService
{
    private readonly string _ffmpegPath;

    public VideoConverterService()
    {
        if (OperatingSystem.IsWindows())
        {
            var ffmpegFolder =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "ffmpeg");

            _ffmpegPath =
                Path.Combine(
                    ffmpegFolder,
                    "ffmpeg.exe");
        }
        else
        {
            _ffmpegPath = "ffmpeg";
        }
    }

    public async Task<string> ConvertToUniversalMp4Async(
        string inputPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var outputPath =
            Path.Combine(
                outputDirectory,
                $"{Path.GetFileNameWithoutExtension(inputPath)}.mp4");

        var ffmpegArgs =
            $"-y -i \"{inputPath}\" " +
            "-c:v libx264 " +
            "-preset medium " +
            "-crf 18 " +
            "-c:a aac " +
            $"\"{outputPath}\"";

        await RunFfmpegAsync(
            ffmpegArgs,
            cancellationToken);

        return outputPath;
    }

    private async Task RunFfmpegAsync(
        string args,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("========== FFMPEG START ==========");
        Console.WriteLine(args);

        using var process = new Process();

        process.StartInfo =
            new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

        process.Start();

        var stdOutTask =
            process.StandardOutput.ReadToEndAsync(
                cancellationToken);

        var stdErrTask =
            process.StandardError.ReadToEndAsync(
                cancellationToken);

        await process.WaitForExitAsync(
            cancellationToken);

        var stdout = await stdOutTask;
        var stderr = await stdErrTask;

        Console.WriteLine(stdout);
        Console.WriteLine(stderr);

        Console.WriteLine("========== FFMPEG COMPLETE ==========");

        if (process.ExitCode != 0)
        {
            throw new Exception(
                $"FFmpeg failed with exit code {process.ExitCode}");
        }
    }
}