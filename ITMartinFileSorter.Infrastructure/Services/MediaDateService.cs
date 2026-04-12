using System.Text.RegularExpressions;
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
            if (FileScanner.ImageExtensions.Contains(ext))
            {
                return ImageMetadataHelper.GetCreationTime(path);
            }

            if (FileScanner.VideoExtensions.Contains(ext))
            {
                return VideoMetadataHelper.GetCreationTime(path);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MEDIA DATE ERROR] {ex.Message}");
        }

        return null;
    }
}