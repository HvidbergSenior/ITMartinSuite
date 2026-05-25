using System.Diagnostics;
using System.Globalization;

namespace ITMartin.Media.Infrastructure.Media;

public abstract class FfmpegServiceBase
{
    protected readonly string FfmpegPath;

    private TimeSpan? _totalDuration;

    private DateTime _lastProgressUpdateUtc;

    protected FfmpegServiceBase()
    {
        if (OperatingSystem.IsWindows())
        {
            var ffmpegFolder =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "ffmpeg");

            FfmpegPath =
                Path.Combine(
                    ffmpegFolder,
                    "ffmpeg.exe");
        }
        else
        {
            FfmpegPath = "ffmpeg";
        }
    }

    protected async Task ExecuteAsync(
        string arguments,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("========== FFMPEG START ==========");
        Console.WriteLine(arguments);

        _totalDuration = null;
        _lastProgressUpdateUtc = DateTime.MinValue;

        using var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName = FfmpegPath,
                        Arguments = arguments,

                        RedirectStandardError = true,
                        RedirectStandardOutput = true,

                        UseShellExecute = false,
                        CreateNoWindow = true
                    },

                EnableRaisingEvents = true
            };

        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(
                    e.Data))
            {
                return;
            }

            ParseProgress(
                e.Data,
                onProgress);
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(
                    e.Data))
            {
                return;
            }

            Console.WriteLine(
                e.Data);
        };

        process.Start();

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

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
            throw new InvalidOperationException(
                $"FFmpeg failed with exit code {process.ExitCode}");
        }
    }

    private void ParseProgress(
        string line,
        Action<double>? onProgress)
    {
        try
        {
            if (line.Contains("Duration:"))
            {
                var durationText =
                    line.Split("Duration:")[1]
                        .Split(",")[0]
                        .Trim();

                if (TimeSpan.TryParse(
                        durationText,
                        CultureInfo.InvariantCulture,
                        out var duration))
                {
                    _totalDuration =
                        duration;
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
                    CultureInfo.InvariantCulture,
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

            onProgress?.Invoke(
                progress);

            Console.WriteLine(
                $"Progress: {progress:P0}");
        }
        catch
        {
            // Ignore malformed FFmpeg output
        }
    }

    protected static string BuildOutputPath(
        string inputPath,
        string suffix)
    {
        var directory =
            Path.GetDirectoryName(
                inputPath)!;

        var fileName =
            Path.GetFileNameWithoutExtension(
                inputPath);

        var extension =
            Path.GetExtension(
                inputPath);

        return Path.Combine(
            directory,
            $"{fileName}.{suffix}{extension}");
    }

    protected static void CopyDates(
        string inputPath,
        string outputPath)
    {
        var created =
            File.GetCreationTimeUtc(
                inputPath);

        var modified =
            File.GetLastWriteTimeUtc(
                inputPath);

        File.SetCreationTimeUtc(
            outputPath,
            created);

        File.SetLastWriteTimeUtc(
            outputPath,
            modified);
    }
}