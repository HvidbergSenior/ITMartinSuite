using System.Diagnostics;

namespace ITMartin.Media.Infrastructure.Media;

public abstract class FfmpegServiceBase
{
    protected readonly string FfmpegPath;

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
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("========== FFMPEG START ==========");
        Console.WriteLine(arguments);

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

        process.Start();

        // IMPORTANT:
        // Read BOTH streams to avoid FFmpeg deadlocks.
        var outputTask =
            process.StandardOutput
                .ReadToEndAsync(cancellationToken);

        var errorTask =
            process.StandardError
                .ReadToEndAsync(cancellationToken);

        await Task.WhenAll(
            outputTask,
            errorTask,
            process.WaitForExitAsync(cancellationToken));

        var output =
            await outputTask;

        var error =
            await errorTask;

        Console.WriteLine("========== FFMPEG COMPLETE ==========");

        if (!string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(output);
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.WriteLine(error);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"FFmpeg failed with exit code {process.ExitCode}{Environment.NewLine}{error}");
        }
    }

    protected static string BuildOutputPath(
        string inputPath,
        string suffix)
    {
        var directory =
            Path.GetDirectoryName(inputPath)!;

        var fileName =
            Path.GetFileNameWithoutExtension(inputPath);

        var extension =
            Path.GetExtension(inputPath);

        return Path.Combine(
            directory,
            $"{fileName}.{suffix}{extension}");
    }

    protected static void CopyDates(
        string inputPath,
        string outputPath)
    {
        var created =
            File.GetCreationTime(inputPath);

        var modified =
            File.GetLastWriteTime(inputPath);

        File.SetCreationTime(
            outputPath,
            created);

        File.SetLastWriteTime(
            outputPath,
            modified);
    }
}