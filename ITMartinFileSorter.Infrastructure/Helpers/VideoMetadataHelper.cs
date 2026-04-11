using System.Diagnostics;
using System.Globalization;

namespace ITMartinFileSorter.Infrastructure.Helpers;

public static class VideoMetadataHelper
{
    public static DateTime? GetCreationTime(string path)
    {
        try
        {
            var ffprobePath = Path.Combine(
                AppContext.BaseDirectory,
                "ffmpeg",
                "ffprobe.exe");

            Console.WriteLine($"FFPROBE PATH: {ffprobePath}");
            Console.WriteLine($"EXISTS: {File.Exists(ffprobePath)}");

            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments =
                    $"-v quiet " +
                    $"-show_entries format_tags=creation_time:stream_tags=creation_time " +
                    $"-of default=noprint_wrappers=1:nokey=1 " +
                    $"\"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            if (process == null)
                return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            Console.WriteLine($"FFPROBE OUTPUT: {output}");

            var firstLine = output
                .Split(Environment.NewLine)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            if (string.IsNullOrWhiteSpace(firstLine))
                return null;

            if (DateTime.TryParse(
                    firstLine,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var dt))
            {
                return dt.ToLocalTime();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FFPROBE ERROR: {ex.Message}");
        }

        return null;
    }
}