using System.Diagnostics;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

public class VideoConverterService : IVideoConverterService
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
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default,
        int ffmpegThreads = 0)
    {
        Directory.CreateDirectory(outputDirectory);

        var outputPath =
            Path.Combine(
                outputDirectory,
                $"{Path.GetFileNameWithoutExtension(inputPath)}.mp4");

        // ffmpegThreads bounds libx264's own internal thread pool - callers
        // running several conversions concurrently (VideoBatchService) pass
        // a value sized so total_processes * threads_per_process stays near
        // the machine's real core count, instead of every process
        // independently grabbing all cores and thrashing. 0 (the default)
        // means "let ffmpeg auto-detect", the original single-conversion
        // behavior.
        var threadsArg = ffmpegThreads > 0 ? $"-threads {ffmpegThreads} " : "";

        var ffmpegArgs =
            $"-hide_banner -y -i \"{inputPath}\" " +
            "-c:v libx264 " +
            threadsArg +
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

        // Per-call local state, not instance fields: VideoBatchService holds
        // one VideoConverterService instance for an entire Package1 run, so
        // once conversions run concurrently, instance fields here would be
        // stomped by whichever video's ffmpeg output line arrived last -
        // corrupting every in-flight conversion's progress percentage.
        TimeSpan? totalDuration = null;
        var lastProgressUpdateUtc = DateTime.MinValue;

        void ParseProgress(string line)
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
                    totalDuration = duration;
                }

                return;
            }

            if (!line.Contains("time=") ||
                !totalDuration.HasValue)
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
                totalDuration.Value.TotalSeconds;

            progress =
                Math.Clamp(
                    progress,
                    0,
                    1);

            if (DateTime.UtcNow -
                lastProgressUpdateUtc <
                TimeSpan.FromSeconds(1))
            {
                return;
            }

            lastProgressUpdateUtc =
                DateTime.UtcNow;

            onProgress?.Invoke(progress);

            Console.WriteLine(
                $"Progress: {progress:P1}");
        }

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

            ParseProgress(e.Data);
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
}