using System.Diagnostics;

namespace ITMartinImageProcessor.Application.Services;

public class HeicToJpgConverterService
{
    public bool NeedsConversion(string file)
    {
        var ext = Path.GetExtension(file).ToLower();
        return ext == ".heic" || ext == ".heif";
    }

    public async Task<string?> ConvertAsync(string inputPath)
    {
        if (!NeedsConversion(inputPath))
            return inputPath;

        var outputPath = Path.ChangeExtension(inputPath, ".jpg");

        if (File.Exists(outputPath))
            return outputPath;

        Console.WriteLine($"[HEIC → JPG] {inputPath}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "magick", // requires ImageMagick installed
                Arguments = $"\"{inputPath}\" \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            Console.WriteLine($"[CONVERT ERROR] {error}");
            return null;
        }

        return outputPath;
    }
} 