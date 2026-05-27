using System.Diagnostics;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class VideoSegmentThumbnailService
    : IVideoSegmentThumbnailService
{
    public async Task<string?> GenerateThumbnailAsync(
        string videoPath,
        TimeSpan timestamp,
        CancellationToken cancellationToken = default)
    {
        var outputFolder =
            Path.Combine(
                Path.GetTempPath(),
                "ITMartinFileSorter",
                "segment-thumbnails");

        Directory.CreateDirectory(outputFolder);

        var fileName =
            $"{Path.GetFileNameWithoutExtension(videoPath)}_{timestamp.TotalSeconds:000000}.jpg";

        var outputPath =
            Path.Combine(
                outputFolder,
                fileName);

        var ffmpegPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "ffmpeg",
                "ffmpeg.exe");

        var args =
            $"-hide_banner -y " +
            $"-ss {timestamp:hh\\:mm\\:ss} " +
            $"-i \"{videoPath}\" " +
            $"-frames:v 1 " +
            $"-q:v 2 " +
            $"\"{outputPath}\"";

        var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = args,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
            };

        process.Start();

        await process.WaitForExitAsync(
            cancellationToken);

        if (!File.Exists(outputPath))
        {
            return null;
        }

        return outputPath;
    }
}