using System.Diagnostics;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Services;

public class ImageConverterService : IImageConverterService
{
    private readonly string _ffmpegPath;

    private static readonly HashSet<string> ConvertibleExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".heic",
            ".heif",
            ".avif"
        };

    public ImageConverterService()
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
            // Docker / Linux / Synology

            _ffmpegPath = "ffmpeg";
        }

        Console.WriteLine(
            $"[IMAGE CONVERTER] FFmpeg: {_ffmpegPath}");
    }

    public bool NeedsConversion(string path)
    {
        var ext =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return ConvertibleExtensions.Contains(ext);
    }

    public bool ShouldKeepOriginal(string path)
    {
        var name =
            Path.GetFileName(path)
                .ToLowerInvariant();

        var ext =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return ext is ".png" or ".jpg" or ".jpeg"
               || name.Contains("screenshot")
               || name.Contains("meme");
    }

    public async Task<string?> ConvertToJpgAsync(
        string inputPath)
    {
        Console.WriteLine(
            "===== IMAGE DEBUG START =====");

        Console.WriteLine(
            $"Input path: {inputPath}");

        if (!File.Exists(inputPath))
        {
            Console.WriteLine(
                "Input file missing");

            return null;
        }

        // =========================
        // KEEP ORIGINALS
        // =========================

        if (ShouldKeepOriginal(inputPath))
        {
            Console.WriteLine(
                "Keeping original");

            return inputPath;
        }

        if (!NeedsConversion(inputPath))
        {
            Console.WriteLine(
                "No conversion needed");

            return inputPath;
        }

        // =========================
        // TEMP NORMALIZED FOLDER
        // =========================

        var tempRoot =
            Path.Combine(
                Path.GetTempPath(),
                "ITMartinFileSorter",
                "images");

        Directory.CreateDirectory(
            tempRoot);

        // =========================
        // SAFE OUTPUT NAME
        // =========================

        var fileName =
            Path.GetFileNameWithoutExtension(
                inputPath);

        var outputPath =
            Path.Combine(
                tempRoot,
                $"{fileName}.jpg");

        Console.WriteLine(
            $"Output path: {outputPath}");

        try
        {
            // Already normalized

            if (File.Exists(outputPath))
            {
                Console.WriteLine(
                    "Already normalized");

                return outputPath;
            }

            await ConvertWithFfmpeg(
                inputPath,
                outputPath);

            if (!File.Exists(outputPath))
            {
                throw new Exception(
                    "JPG not created");
            }

            CopyDates(
                inputPath,
                outputPath);

            Console.WriteLine(
                "===== IMAGE DEBUG END =====");

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[IMAGE CONVERT ERROR] {ex}");

            return inputPath;
        }
    }

    private async Task ConvertWithFfmpeg(
        string inputPath,
        string outputPath)
    {
        if (OperatingSystem.IsWindows() &&
            !File.Exists(_ffmpegPath))
        {
            throw new FileNotFoundException(
                "FFmpeg not found",
                _ffmpegPath);
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments =
                    $"-y -i \"{inputPath}\" -frames:v 1 \"{outputPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        var tcs = new TaskCompletionSource<int>();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Console.WriteLine(e.Data);
            }
        };

        process.Exited += (_, _) =>
        {
            tcs.TrySetResult(process.ExitCode);
        };

        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exitCode =
            await tcs.Task;

        Console.WriteLine(
            $"[IMAGE FFMPEG EXIT] {exitCode}");

        if (exitCode != 0)
        {
            throw new Exception(
                $"FFmpeg failed: {exitCode}");
        }
    }

    private void CopyDates(
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