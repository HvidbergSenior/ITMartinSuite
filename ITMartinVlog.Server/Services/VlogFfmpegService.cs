using System.Diagnostics;

namespace ITMartinVlog.Server.Services;

// Direct ffmpeg calls for the two actions that don't need a full Package4
// workflow pass (splitting audio out, grabbing still frames for the AI
// Q&A panel) - a full WorkflowStep would be overkill for a single -vn/-ss call.
public sealed class VlogFfmpegService
{
    private readonly string _ffmpegPath = OperatingSystem.IsWindows()
        ? Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe")
        : "ffmpeg";

    public async Task<string> ExtractAudioAsync(string videoPath, string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(videoPath) + ".mp3");

        await RunAsync($"-y -i \"{videoPath}\" -vn -c:a libmp3lame -b:a 192k \"{outputPath}\"", cancellationToken);
        return outputPath;
    }

    public async Task<List<string>> ExtractFramesAsync(string videoPath, string outputDirectory, int count, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var baseName = Path.GetFileNameWithoutExtension(videoPath) + "-" + Guid.NewGuid().ToString("N")[..8];
        var pattern = Path.Combine(outputDirectory, baseName + "-%02d.jpg");

        await RunAsync(
            $"-y -i \"{videoPath}\" -vf \"select='not(mod(n\\,round(30/{count})))',scale=480:-1\" -frames:v {count} -q:v 4 \"{pattern}\"",
            cancellationToken);

        return Directory.EnumerateFiles(outputDirectory, baseName + "-*.jpg")
            .OrderBy(f => f)
            .ToList();
    }

    private async Task RunAsync(string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg fejlede (kode {process.ExitCode}): {stderr}");
        }
    }
}
