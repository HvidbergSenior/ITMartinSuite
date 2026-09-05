using System.Diagnostics;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

public class VideoConverterService : IVideoConverterService
{
    // Safety net, not a normal ceiling - a malformed/corrupt source can make
    // ffmpeg hang indefinitely instead of erroring out (confirmed 2026-09-06
    // on this same codebase's AudioConverterService and VideoMetadataService,
    // both fixed the same way). Unlike audio, a real video conversion can
    // legitimately run long for a large/long file, so this is deliberately
    // generous (30 min) rather than tuned to a "normal" duration - it exists
    // to bound the worst case, not to flag merely-slow conversions.
    private static readonly TimeSpan FfmpegTimeout = TimeSpan.FromMinutes(30);

    // Caches whether the GPU encoder actually works on this machine after the
    // first attempt, so a GPU-less deployment (the NAS/photoserver Docker
    // containers - see repo CLAUDE.md, neither has this GPU mapped through)
    // doesn't retry-and-fail h264_amf on every single video, just the first
    // few concurrent ones while the cache converges. null = untested yet.
    private static bool? _amfAvailable;

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

        // Try the GPU (AMD AMF hardware encoder) first - measured ~5x faster
        // than libx264 on this suite's actual hardware (RX 7800 XT,
        // 2026-09-04) and barely touches the CPU, which matters here because
        // the CPU is the pipeline's shared bottleneck resource (hashing,
        // image quality, dedup, and up to DegreeOfParallelism other
        // conversions all compete for it at the same time - see
        // ConcurrentVideoDispatcher). -rc cqp with these QP values targets
        // roughly the same output size as libx264's -crf 22 below, not
        // AMF's much larger bitrate-mode default (measured ~2.5x bigger
        // with no rate-control flags at all).
        if (_amfAvailable != false)
        {
            var amfArgs =
                $"-hide_banner -y -i \"{inputPath}\" " +
                "-c:v h264_amf -rc cqp -qp_i 22 -qp_p 24 -qp_b 24 -quality quality " +
                "-pix_fmt yuv420p " +
                "-c:a aac " +
                "-movflags +faststart " +
                "-stats " +
                $"\"{outputPath}\"";

            try
            {
                await RunFfmpegAsync(amfArgs, onProgress, cancellationToken);
                _amfAvailable = true;
                return outputPath;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Not every deployment has this GPU (the NAS/photoserver
                // Docker containers don't) - h264_amf simply fails to
                // initialize there. Every video falls back to the
                // always-available libx264 path below exactly as before.
                _amfAvailable = false;
                Console.WriteLine(
                    $"h264_amf unavailable/failed ({ex.Message}) - falling back to libx264 for {inputPath}");
            }
        }

        // ffmpegThreads bounds libx264's own internal thread pool - callers
        // running several conversions concurrently (ConcurrentVideoDispatcher) pass
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

        // Per-call local state, not instance fields: ConcurrentVideoDispatcher holds
        // one VideoConverterService instance for an entire QuickSort run, so
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

        using var timeoutCts = new CancellationTokenSource(FfmpegTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(
                linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("FFMPEG CANCELLATION REQUESTED");
                throw;
            }

            throw new TimeoutException($"FFmpeg (video) exceeded {FfmpegTimeout} and was killed - likely a corrupt/malformed source file, not a slow one.");
        }

        Console.WriteLine("========== FFMPEG COMPLETE ==========");

        if (process.ExitCode != 0)
        {
            throw new Exception(
                $"FFmpeg failed with exit code {process.ExitCode}");
        }
    }
}