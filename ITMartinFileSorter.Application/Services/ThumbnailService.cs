using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ITMartinFileSorter.Application.Services;

public class ThumbnailService
{
    private readonly string _thumbnailRoot;
    private readonly string _webThumbnailPath = "/media_temp/thumbnails";

    public ThumbnailService()
    {
        _thumbnailRoot = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot",
            "media_temp",
            "thumbnails");

        Directory.CreateDirectory(_thumbnailRoot);
    }

    public string? GenerateThumbnail(MediaFile file)
    {
        try
        {
            if (!File.Exists(file.FullPath))
                return null;

            var fileName = GetThumbnailFileName(file.FullPath);
            var fullOutputPath = Path.Combine(_thumbnailRoot, fileName);

            if (!File.Exists(fullOutputPath))
            {
                switch (file.Type)
                {
                    case MediaType.Image:
                        GenerateImageThumbnail(file.FullPath, fullOutputPath);
                        break;

                    case MediaType.Video:
                        GenerateVideoThumbnail(file.FullPath, fullOutputPath);
                        break;

                    default:
                        return null;
                }
            }

            return $"{_webThumbnailPath}/{fileName}";
        }
        catch
        {
            return null;
        }
    }

    private string GetThumbnailFileName(string fullPath)
    {
        var cacheKey =
            $"{fullPath}_{File.GetLastWriteTimeUtc(fullPath):O}";

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(cacheKey));

        return Convert.ToHexString(hash) + ".jpg";
    }

    private void GenerateImageThumbnail(string inputPath, string outputPath)
    {
        using var image = Image.Load(inputPath);

        image.Mutate(x =>
            x.Resize(new ResizeOptions
            {
                Size = new Size(320, 180),
                Mode = ResizeMode.Max
            }));

        image.SaveAsJpeg(outputPath);
    }

    private void GenerateVideoThumbnail(string inputPath, string outputPath)
    {
        var ffmpegPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "ffmpeg",
            "ffmpeg.exe");

        if (!File.Exists(ffmpegPath))
            return;

        using var process = new Process();

        process.StartInfo.FileName = ffmpegPath;
        process.StartInfo.Arguments =
            $"-y -ss 00:00:05 -i \"{inputPath}\" -vframes 1 -vf scale=320:-1 \"{outputPath}\"";

        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;

        process.Start();
        process.WaitForExit();
    }
}