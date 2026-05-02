using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Infrastructure.FileSystem;

public sealed class FileScanner : IFileScanner
{
private readonly IFileSystem _fileSystem;
private readonly IHashService _hashService;
private readonly IMediaDateService _mediaDateService;
private readonly IMediaClassificationService _classificationService;
private readonly IExifService _exifService;

public FileScanner(
    IFileSystem fileSystem,
    IHashService hashService,
    IMediaDateService mediaDateService,
    IMediaClassificationService classificationService,
    IExifService exifService)
{
    _fileSystem = fileSystem;
    _hashService = hashService;
    _mediaDateService = mediaDateService;
    _classificationService = classificationService;
    _exifService = exifService;
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
        !_fileSystem.DirectoryExists(rootPath))
        yield break;

    int index = 0;

    foreach (var file in _fileSystem.EnumerateFiles(rootPath, "*.*", _options))
    {
        index++;
        onProgress?.Invoke(index, file);

        if (!IsSupportedMedia(file))
            continue;

        long size;
        DateTime lastWrite;

        try
        {
            size = _fileSystem.GetFileSize(file);
            lastWrite = _fileSystem.GetLastWriteTime(file);
        }
        catch
        {
            continue;
        }

        var ext = Path.GetExtension(file);

        var type =
            IsImage(file) ? MediaType.Image :
            IsVideo(file) ? MediaType.Video :
            AudioExtensions.Contains(ext)
                ? MediaType.Audio
                : MediaType.Document;

        // ===== DATE =====
        DateTime? bestDate = null;
        bool isReliable = false;

        try
        {
            var result = _mediaDateService.GetBestDate(file);
            bestDate = result.date;
            isReliable = result.isReliable;
        }
        catch
        {
            // ignore
        }

        var finalDate = bestDate ?? lastWrite;

        // ===== CREATE =====
        var mediaFile = new MediaFile(
            fullPath: file,
            createdAt: finalDate,
            type: type,
            sizeBytes: size
        );

        mediaFile.SetDate(finalDate, isReliable);

        // ===== IMAGE METADATA =====
        if (type == MediaType.Image)
        {
            try
            {
                var (w, h) = _exifService.GetDimensions(file);
                mediaFile.Width = w;
                mediaFile.Height = h;
                mediaFile.HasExif = w != null && h != null;
            }
            catch
            {
                // ignore
            }
        }

        // ===== HASH =====
        try
        {
            mediaFile.SetHash(_hashService.ComputeHash(file));
        }
        catch
        {
            // ignore
        }

        // ===== CLASSIFICATION =====
        try
        {
            _classificationService.Classify(mediaFile);
        }
        catch
        {
            // NEVER break scan
        }

        yield return mediaFile;
    }
}

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

}
