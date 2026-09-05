using System.Diagnostics;
using System.Globalization;
using System.Text;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Metadata;

public class VideoMetadataService : IVideoMetadataService
{
    // Some malformed/edge-case video files (bad atom structure, truncated
    // streams, etc.) send ffprobe into what is effectively an infinite loop
    // internally - confirmed 2026-09-05 on a RicoAC .mp4 that read back fine
    // byte-for-byte (md5sum completed instantly) yet hung ffprobe for over an
    // hour with zero progress. WaitForExit() with no timeout has no way to
    // recover from that, so a single bad file among tens of thousands can
    // stall an entire library scan forever.
    private static readonly TimeSpan FfprobeTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<VideoMetadataService> _logger;
    public VideoMetadataService(
        ILogger<VideoMetadataService> logger)
    {
        _logger = logger;
    }
    public DateTime? GetCreationTime(string path)
    {
        try
        {
            var ffprobePath = OperatingSystem.IsWindows()
                ? Path.Combine(
                    AppContext.BaseDirectory,
                    "ffmpeg",
                    "ffprobe.exe")
                : "ffprobe";
            _logger.LogInformation(
                "Running ffprobe for {Path}",
                path);
            Console.WriteLine("========== FFPROBE START ==========");
            Console.WriteLine($"[INPUT PATH] {path}");
            Console.WriteLine($"[FILE EXISTS] {File.Exists(path)}");
            Console.WriteLine($"[WORKING DIR] {Environment.CurrentDirectory}");
            Console.WriteLine($"[FFPROBE CMD] {ffprobePath}");

            var arguments =
                "-v quiet " +
                "-show_entries format_tags=creation_time:stream_tags=creation_time " +
                "-of default=noprint_wrappers=1:nokey=1 " +
                $"\"{path}\"";

            Console.WriteLine($"[FFPROBE ARGS] {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var result = RunFfprobeWithTimeout(psi, path, _logger);

            if (result is null)
            {
                _logger.LogWarning(
                    "ffprobe returned empty output for {Path}",
                    path);
                return null;
            }

            var (exitCode, output, error) = result.Value;

            Console.WriteLine($"[EXIT CODE] {exitCode}");

            Console.WriteLine("----- STDOUT -----");
            Console.WriteLine(output);

            Console.WriteLine("----- STDERR -----");
            Console.WriteLine(error);

            if (exitCode != 0)
            {
                Console.WriteLine("[FFPROBE ERROR] Non-zero exit code");
                return null;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine("[FFPROBE ERROR] Empty output");
                return null;
            }

            var firstLine = output
                .Split(Environment.NewLine)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            Console.WriteLine($"[PARSED LINE] {firstLine}");

            if (string.IsNullOrWhiteSpace(firstLine))
                return null;

            if (DateTime.TryParse(
                    firstLine,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var dt))
            {
                var local = dt.ToLocalTime();
                return local;
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFPROBE EXCEPTION] {ex}");
        }

        return null;
    }

    // Starts both stream reads before waiting on the process, so there's no
    // risk of the classic ReadToEnd-then-WaitForExit deadlock (a process that
    // fills the unread stderr/stdout OS pipe buffer while nothing is draining
    // it) on top of the timeout/kill protection against a genuinely hung probe.
    private static (int ExitCode, string StdOut, string StdErr)? RunFfprobeWithTimeout(ProcessStartInfo psi, string path, ILogger logger)
    {
        using var process = Process.Start(psi);
        if (process is null) return null;

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)FfprobeTimeout.TotalMilliseconds))
        {
            logger.LogWarning("ffprobe timed out after {Timeout} on {Path} - killing and treating as unreadable", FfprobeTimeout, path);
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return null;
        }

        return (process.ExitCode, stdOutTask.GetAwaiter().GetResult(), stdErrTask.GetAwaiter().GetResult());
    }

    public string GetModelFromFileName(string path)
    {
        var fileName = Path.GetFileName(path).ToUpperInvariant();

        if (fileName.StartsWith("VID_") || fileName.StartsWith("MVI_"))
            return "Camera";

        return "Unknown";
    }

    public TimeSpan? GetDuration(string path)
    {
        try
        {
            var ffprobePath = OperatingSystem.IsWindows()
                ? Path.Combine(
                    AppContext.BaseDirectory,
                    "ffmpeg",
                    "ffprobe.exe")
                : "ffprobe";

            var arguments =
                "-v error " +
                "-show_entries format=duration " +
                "-of default=noprint_wrappers=1:nokey=1 " +
                $"\"{path}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var result = RunFfprobeWithTimeout(psi, path, _logger);

            if (result is null)
            {
                return null;
            }

            var output = result.Value.StdOut;

            if (double.TryParse(
                    output.Trim(),
                    CultureInfo.InvariantCulture,
                    out var seconds))
            {
                return TimeSpan.FromSeconds(
                    seconds);
            }
        }
        catch
        {
        }

        return null;
        
    }

    public (int Width, int Height)? GetDimensions(string path)
    {
        throw new NotImplementedException();
    }

    // A ".mp4" extension only means "MP4 container" - it says nothing about
    // what's actually encoded inside. Old camcorders/point-and-shoots often
    // wrote MPEG-4 Part 2 ("mpeg4") or other codecs into an .mp4 container,
    // and no browser's <video> element decodes those, only H.264/H.265/VP8/
    // VP9/AV1. Trusting the extension alone (as MediaRulesWorkflowStep used
    // to) let non-web-safe video silently skip normalization.
    public string? GetVideoCodec(string path)
    {
        try
        {
            var ffprobePath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffprobe.exe")
                : "ffprobe";

            var arguments =
                "-v error " +
                "-select_streams v:0 " +
                "-show_entries stream=codec_name " +
                "-of default=noprint_wrappers=1:nokey=1 " +
                $"\"{path}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var result = RunFfprobeWithTimeout(psi, path, _logger);
            if (result is null) return null;

            var (exitCode, output, _) = result.Value;
            if (exitCode != 0) return null;

            var codec = output.Trim();
            return string.IsNullOrWhiteSpace(codec) ? null : codec;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetVideoCodec failed for {Path}", path);
            return null;
        }
    }

    // Moved here from LibraryPolishService's late, post-export
    // QuarantineUnplayableVideos pass (2026-09-06) - confirmed on Rico/AC's
    // archive that a corrupt file was going through hashing, metadata,
    // export, conversion AND thumbnail generation before finally landing in
    // Review at the very end, wasting the most expensive steps in the whole
    // pipeline on a file that gets thrown out anyway. Called from
    // MediaRulesWorkflowStep instead, before any of that runs.
    //
    // `-t 3` caps ffmpeg's own decode work to 3 seconds regardless of file
    // length, but a malformed file can still hang on OPEN rather than decode
    // - same failure class as the ffprobe hang this class already guards
    // against elsewhere, so this goes through the same timeout helper.
    //
    // One retry after a short delay: confirmed 2026-09-03 that 94% of
    // quarantines from a single attempt were false positives caused by
    // resource contention (many ffmpeg processes running concurrently for
    // conversion/thumbnails at the same time as this check) rather than real
    // corruption. Calling this earlier, before that concurrent work starts,
    // should make the retry matter less than it used to - kept anyway since
    // it's nearly free insurance.
    public bool CanDecodeStart(string path)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            if (TryDecodeStart(path)) return true;
            if (attempt < 2) Thread.Sleep(500);
        }

        return false;
    }

    private bool TryDecodeStart(string path)
    {
        try
        {
            var ffmpegPath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe")
                : "ffmpeg";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                ArgumentList = { "-v", "error", "-xerror", "-t", "3", "-i", path, "-f", "null", "-" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var result = RunFfprobeWithTimeout(psi, path, _logger);
            return result is not null && result.Value.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CanDecodeStart failed for {Path}", path);
            return false;
        }
    }
}