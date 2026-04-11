using ITMartinFileSorter.Domain.Interfaces;
using ITMartinFileSorter.Infrastructure.FileSystem;
using ITMartinFileSorter.Infrastructure.Helpers;

namespace ITMartinFileSorter.Infrastructure.Services;

public class MediaDateService : IMediaDateService
{
    public DateTime GetBestDate(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        try
        {
            if (FileScanner.ImageExtensions.Contains(ext))
            {
                var imgDate = ImageMetadataHelper.GetCreationTime(path);
                if (imgDate != null)
                    return imgDate.Value;
            }

            if (FileScanner.VideoExtensions.Contains(ext))
            {
                var videoDate = VideoMetadataHelper.GetCreationTime(path);
                if (videoDate != null)
                    return videoDate.Value;
            }
        }
        catch
        {
            // ignore
        }

        return File.GetLastWriteTime(path);
    }
}