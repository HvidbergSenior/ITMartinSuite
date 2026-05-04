using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ITMartinFileSorter.Infrastructure.Helpers;

public static class VideoMetadataHelper
{
    public static DateTime? GetCreationTime(string path)
    {
        try
        {
            var ffprobePath = OperatingSystem.IsWindows()
                ? "ffprobe.exe"
                : "ffprobe";

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
                Console.WriteLine("[FFPROBE ERROR] Failed to start process");
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
                Console.WriteLine($"[VIDEO DATE] {local}");
                Console.WriteLine("========== FFPROBE SUCCESS ==========");
                return local;
            }

            Console.WriteLine("[FFPROBE ERROR] Failed to parse date");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFPROBE EXCEPTION] {ex}");
        }

        Console.WriteLine("========== FFPROBE END (NULL) ==========");
        return null;
    }

    public static string GetModelFromFileName(string path)
    {
        var fileName = Path.GetFileName(path).ToUpperInvariant();

        if (fileName.StartsWith("VID_") || fileName.StartsWith("MVI_"))
            return "Camera";

        return "Unknown";
    }
}