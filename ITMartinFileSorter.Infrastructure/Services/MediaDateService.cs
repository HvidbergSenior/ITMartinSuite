using ITMartinFileSorter.Domain.Interfaces;
using ITMartinFileSorter.Infrastructure.FileSystem;
using ITMartinFileSorter.Infrastructure.Helpers;

namespace ITMartinFileSorter.Infrastructure.Services;

public class MediaDateService : IMediaDateService
{
    public DateTime? GetBestDate(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        try
        {
            // Images
            if (FileScanner.ImageExtensions.Contains(ext))
            {
                return ImageMetadataHelper.GetCreationTime(path)
                       ?? GetFileFallbackDate(path);
            }

            // Videos
            if (FileScanner.VideoExtensions.Contains(ext))
            {
                return VideoMetadataHelper.GetCreationTime(path)
                       ?? GetFileFallbackDate(path);
            }

            // Documents
            if (FileScanner.DocumentExtensions.Contains(ext))
            {
                return DocumentMetadataHelper.GetCreationTime(path)
                       ?? GetFileFallbackDate(path);
            }

            // Audio
            if (FileScanner.AudioExtensions.Contains(ext))
            {
                return GetFileFallbackDate(path);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MEDIA DATE ERROR] {ex.Message}");
        }

        return GetFileFallbackDate(path);
    }

    private DateTime? GetFileFallbackDate(string path)
    {
        try
        {
            var info = new FileInfo(path);

            // Prefer the oldest meaningful date
            return info.CreationTime < info.LastWriteTime
                ? info.CreationTime
                : info.LastWriteTime;
        }
        catch
        {
            return null;
        }
    }
}