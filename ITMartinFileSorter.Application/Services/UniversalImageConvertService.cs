using System.Diagnostics;

namespace ITMartinFileSorter.Application.Services;

public class UniversalImageConverterService
{
    private readonly string _ffmpegPath;

    private static readonly HashSet<string> ConvertibleExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".heic",
            ".heif",
            ".avif"
        };

    public UniversalImageConverterService()
    {
        _ffmpegPath = Path.Combine(
            AppContext.BaseDirectory,
            "ffmpeg",
            "ffmpeg.exe");

        Console.WriteLine($"[IMAGE CONVERTER] FFmpeg: {_ffmpegPath}");
        Console.WriteLine($"[IMAGE CONVERTER] Exists: {File.Exists(_ffmpegPath)}");
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

            TryDeleteOriginal(inputPath, outputPath);

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

    private void TryDeleteOriginal(
        string inputPath,
        string outputPath)
    {
        if (!File.Exists(outputPath))
            return;

        if (string.Equals(inputPath, outputPath,
            StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            File.Delete(inputPath);

            Console.WriteLine($"[IMAGE ORIGINAL DELETED] {inputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DELETE FAILED] {ex}");
        }
    }
}