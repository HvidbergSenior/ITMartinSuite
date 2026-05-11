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
        _ffmpegPath = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        Console.WriteLine($"[IMAGE CONVERTER] FFmpeg: {_ffmpegPath}");
    }

    public bool NeedsConversion(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        return ConvertibleExtensions.Contains(ext);
    }

    public bool ShouldKeepOriginal(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        var ext = Path.GetExtension(path).ToLowerInvariant();

        return ext is ".png" or ".jpg" or ".jpeg" ||
               name.Contains("screenshot") ||
               name.Contains("meme");
    }

    public async Task<string?> ConvertToJpgAsync(string inputPath)
    {
        Console.WriteLine("===== IMAGE DEBUG START =====");
        Console.WriteLine($"Input path: {inputPath}");

        if (!File.Exists(inputPath))
        {
            Console.WriteLine("Input file missing");
            return null;
        }

        if (ShouldKeepOriginal(inputPath))
        {
            Console.WriteLine("Keeping original");
            return inputPath;
        }

        if (!NeedsConversion(inputPath))
        {
            Console.WriteLine("No conversion needed");
            return inputPath;
        }

        var outputPath = Path.ChangeExtension(inputPath, ".jpg");

        Console.WriteLine($"Output path: {outputPath}");

        try
        {
            await ConvertWithFfmpeg(inputPath, outputPath);

            if (!File.Exists(outputPath))
                throw new Exception("JPG not created");

            CopyDates(inputPath, outputPath);

            await SafeDeleteAsync(inputPath);

            Console.WriteLine("===== IMAGE DEBUG END =====");

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IMAGE CONVERT ERROR] {ex}");

            return inputPath;
        }
    }

    private async Task ConvertWithFfmpeg(
        string inputPath,
        string outputPath)
    {
        if (!File.Exists(_ffmpegPath))
            throw new FileNotFoundException(
                "FFmpeg not found",
                _ffmpegPath);

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
                Console.WriteLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                Console.WriteLine(e.Data);
        };

        process.Exited += (_, _) =>
        {
            tcs.TrySetResult(process.ExitCode);
        };

        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exitCode = await tcs.Task;

        Console.WriteLine($"[IMAGE FFMPEG EXIT] {exitCode}");

        if (exitCode != 0)
            throw new Exception($"FFmpeg failed: {exitCode}");
    }

    private void CopyDates(string inputPath, string outputPath)
    {
        var created = File.GetCreationTime(inputPath);
        var modified = File.GetLastWriteTime(inputPath);

        File.SetCreationTime(outputPath, created);
        File.SetLastWriteTime(outputPath, modified);
    }
    private async Task SafeDeleteAsync(string path)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                File.Delete(path);

                Console.WriteLine($"[IMAGE DELETE SUCCESS] {path}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IMAGE DELETE RETRY {i}] {ex.Message}");
                await Task.Delay(300);
            }
        }

        Console.WriteLine($"[IMAGE DELETE FAILED] {path}");
    }

    
}