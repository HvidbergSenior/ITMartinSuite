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
                    }
            };

        process.Start();

        var error =
            await process.StandardError
                .ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(
            cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                error);
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