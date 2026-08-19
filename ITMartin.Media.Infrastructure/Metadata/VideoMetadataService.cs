using System.Diagnostics;
using System.Globalization;
using System.Text;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Metadata;

public class VideoMetadataService : IVideoMetadataService
{
    
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

            using var process = Process.Start(psi);

            if (process == null)
            {
                _logger.LogWarning(
                    "ffprobe returned empty output for {Path}",
                    path);
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            Console.WriteLine($"[EXIT CODE] {process.ExitCode}");

            Console.WriteLine("----- STDOUT -----");
            Console.WriteLine(output);

            Console.WriteLine("----- STDERR -----");
            Console.WriteLine(error);

            if (process.ExitCode != 0)
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

            using var process =
                Process.Start(psi);

            if (process is null)
            {
                return null;
            }

            var output =
                process.StandardOutput
                    .ReadToEnd();

            process.WaitForExit();

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

            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0) return null;

            var codec = output.Trim();
            return string.IsNullOrWhiteSpace(codec) ? null : codec;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetVideoCodec failed for {Path}", path);
            return null;
        }
    }
}