using System.Diagnostics;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

public class VideoConverterService : IVideoConverterService
{
    private readonly string _ffmpegPath;

    private TimeSpan? _totalDuration;

    private DateTime _lastProgressUpdateUtc;

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
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var outputPath =
            Path.Combine(
                outputDirectory,
                $"{Path.GetFileNameWithoutExtension(inputPath)}.mp4");

        var ffmpegArgs =
            $"-hide_banner -y -i \"{inputPath}\" " +
            "-c:v libx264 " +
            "-pix_fmt yuv420p " +
            "-preset veryfast " +
            "-crf 22 " +
            "-c:a aac " +
            "-movflags +faststart " +
            "-stats " +
            $"\"{outputPath}\"";

        await RunFfmpegAsync(
            ffmpegArgs,
            onProgress,
            cancellationToken);

        return outputPath;
    }

    private async Task RunFfmpegAsync(
        string args,
        Action<double>? onProgress,
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

        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            ParseProgress(
                e.Data,
                onProgress);
        };

        process.Start();

        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine(
                "FFMPEG CANCELLATION REQUESTED");

            if (!process.HasExited)
            {
                process.Kill(true);
            }

            throw;
        }

        Console.WriteLine("========== FFMPEG COMPLETE ==========");

        if (process.ExitCode != 0)
        {
            throw new Exception(
                $"FFmpeg failed with exit code {process.ExitCode}");
        }
    }

    private void ParseProgress(
        string line,
        Action<double>? onProgress)
    {
        if (line.Contains("Duration:"))
        {
            var durationText =
                line.Split("Duration:")[1]
                    .Split(",")[0]
                    .Trim();

            if (TimeSpan.TryParse(
                    durationText,
                    out var duration))
            {
                _totalDuration = duration;
            }

            return;
        }

        if (!line.Contains("time=") ||
            !_totalDuration.HasValue)
        {
            return;
        }

        var timeText =
            line.Split("time=")[1]
                .Split(" ")[0]
                .Trim();

        if (!TimeSpan.TryParse(
                timeText,
                out var current))
        {
            return;
        }

        var progress =
            current.TotalSeconds /
            _totalDuration.Value.TotalSeconds;

        progress =
            Math.Clamp(
                progress,
                0,
                1);

        if (DateTime.UtcNow -
            _lastProgressUpdateUtc <
            TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastProgressUpdateUtc =
            DateTime.UtcNow;

        onProgress?.Invoke(progress);

        Console.WriteLine(
            $"Progress: {progress:P1}");
    }
}