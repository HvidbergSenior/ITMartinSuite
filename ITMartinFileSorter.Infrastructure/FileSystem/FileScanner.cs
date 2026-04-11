using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Infrastructure.FileSystem;

public sealed class FileScanner : IFileScanner
{
    private readonly IHashService _hashService;

    public FileScanner(IHashService hashService)
    {
        _hashService = hashService;
    }
    public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp",
        ".webp", ".tiff", ".tif", ".heic", ".heif", ".avif"
    };

    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv"
    };

    public static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg"
    };

    public static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".txt",
        ".xls", ".xlsx", ".ppt", ".pptx", ".csv"
    };

    public static bool IsSupportedMedia(string path)
    {
        var extension = Path.GetExtension(path);

        return ImageExtensions.Contains(extension) ||
               VideoExtensions.Contains(extension) ||
               AudioExtensions.Contains(extension) ||
               DocumentExtensions.Contains(extension);
    }

    public static bool IsImage(string path)
        => ImageExtensions.Contains(Path.GetExtension(path));

    public static bool IsVideo(string path)
        => VideoExtensions.Contains(Path.GetExtension(path));

    private readonly EnumerationOptions _options = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false
    };

    public IEnumerable<MediaFile> ScanFolder(
        string rootPath,
        Action<int, string>? onProgress = null)
    {
        if (string.IsNullOrWhiteSpace(rootPath) ||
            !Directory.Exists(rootPath))
            yield break;

        int index = 0;

        foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", _options))
        {
            index++;
            onProgress?.Invoke(index, file);

            if (!IsSupportedMedia(file))
                continue;

            FileInfo info;

            try
            {
                info = new FileInfo(file);
            }
            catch
            {
                continue;
            }

            MediaType type =
                IsImage(file) ? MediaType.Image :
                IsVideo(file) ? MediaType.Video :
                AudioExtensions.Contains(Path.GetExtension(file))
                    ? MediaType.Audio
                    : MediaType.Document;

            var mediaFile = new MediaFile(
                fullPath: file,
                createdAt: info.LastWriteTimeUtc,
                type: type,
                sizeBytes: info.Length
            );

            mediaFile.SetHash( _hashService.ComputeHash(file));

            yield return mediaFile;
        }
    }
}