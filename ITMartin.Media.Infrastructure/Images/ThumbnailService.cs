using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ITMartin.Media.Infrastructure.Images;

public sealed class ThumbnailService
    : IThumbnailService
{
    private readonly string _thumbnailRoot;

    private readonly ILogger<ThumbnailService>
        _logger;

    public ThumbnailService(
        ILogger<ThumbnailService> logger)
    {
        _logger = logger;

        _thumbnailRoot =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "media_temp",
                "thumbnails");

        Directory.CreateDirectory(
            _thumbnailRoot);
    }

    public async Task<string> GenerateAsync(
        string sourcePath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Thumbnail generation started for {File}",
            sourcePath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                "Source file not found.",
                sourcePath);
        }

        var extension =
            Path.GetExtension(sourcePath)
                .ToLowerInvariant();

        if (extension is ".jpg"
            or ".jpeg"
            or ".png")
        {
            await GenerateImageThumbnailAsync(
                sourcePath,
                outputPath,
                cancellationToken);
        }
        else if (extension is
                 ".mp4"
                 or ".mkv"
                 or ".avi"
                 or ".mov"
                 or ".mpg"
                 or ".mpeg"
                 or ".mts"
                 or ".m2ts"
                 or ".wmv")
        {
            await GenerateVideoThumbnailAsync(
                sourcePath,
                outputPath,
                cancellationToken);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported thumbnail format: {extension}");
        }

        _logger.LogInformation(
            "Thumbnail generation completed for {File}",
            sourcePath);

        return outputPath;
    }

    private static async Task
        GenerateImageThumbnailAsync(
            string inputPath,
            string outputPath,
            CancellationToken cancellationToken)
    {
        using var image =
            await Image.LoadAsync(
                inputPath,
                cancellationToken);

        image.Mutate(x =>
            x.Resize(
                new ResizeOptions
                {
                    Size =
                        new Size(300, 300),

                    Mode =
                        ResizeMode.Max
                }));

        await image.SaveAsJpegAsync(
            outputPath,
            cancellationToken);
    }

    private async Task
        GenerateVideoThumbnailAsync(
            string inputPath,
            string outputPath,
            CancellationToken cancellationToken)
    {
        var ffmpegPath =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "ffmpeg",
                "ffmpeg.exe");

        if (!File.Exists(ffmpegPath))
        {
            throw new FileNotFoundException(
                "ffmpeg executable not found.",
                ffmpegPath);
        }

        using var cts =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        cts.CancelAfter(
            TimeSpan.FromMinutes(2));

        using var process =
            new Process();

        process.StartInfo.FileName =
            ffmpegPath;

        process.StartInfo.Arguments =
            $"-y -ss 00:00:05 -i \"{inputPath}\" -vframes 1 -vf scale=320:-1 \"{outputPath}\"";

        process.StartInfo.CreateNoWindow =
            true;

        process.StartInfo.UseShellExecute =
            false;

        process.StartInfo.RedirectStandardError =
            true;

        process.StartInfo.RedirectStandardOutput =
            true;

        _logger.LogInformation(
            "Starting ffmpeg thumbnail generation for {File}",
            inputPath);

        process.Start();

        var stdoutTask =
            process.StandardOutput
                .ReadToEndAsync(cts.Token);

        var stderrTask =
            process.StandardError
                .ReadToEndAsync(cts.Token);

        await process.WaitForExitAsync(
            cts.Token);

        var stdout =
            await stdoutTask;

        var stderr =
            await stderrTask;

        _logger.LogInformation(
            "Completed ffmpeg thumbnail generation for {File}",
            inputPath);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"ffmpeg thumbnail generation failed.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }
    }

    private static string GetThumbnailFileName(
        string fullPath)
    {
        var cacheKey =
            $"{fullPath}_{File.GetLastWriteTimeUtc(fullPath):O}";

        using var sha =
            SHA256.Create();

        var hash =
            sha.ComputeHash(
                Encoding.UTF8.GetBytes(cacheKey));

        return Convert.ToHexString(hash)
               + ".jpg";
    }
}