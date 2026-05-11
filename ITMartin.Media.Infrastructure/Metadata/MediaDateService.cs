using ITMartin.Media.Infrastructure.FileSystem;
using ITMartin.Media.Infrastructure.Videos;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Infrastructure.Metadata;

public class MediaDateService : IMediaDateService
{
    private readonly IImageMetadataService _imageMetadataService;
    private readonly IVideoMetadataService _videoMetadataService;
    private readonly IDocumentMetadataService _documentMetadataService;
        

    public MediaDateService(IImageMetadataService imageMetadataService, IVideoMetadataService videoMetadataService, IDocumentMetadataService documentMetadataService)
    {
        _imageMetadataService = imageMetadataService;
        _videoMetadataService = videoMetadataService;
        _documentMetadataService = documentMetadataService;
    }

    public (DateTime? date, bool isReliable) GetBestDate(string path)
    {
        // ✅ 1. Filename (HIGH TRUST)
        var fileNameDate = TryParseDateFromFileName(path);
        if (fileNameDate != null)
        {
            Console.WriteLine($"[FILENAME DATE] {Path.GetFileName(path)} -> {fileNameDate}");
            return (fileNameDate, true);
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();

        try
        {
            // Images
            if (FileScanner.ImageExtensions.Contains(ext))
            {
                var date = _imageMetadataService.GetCreationTime(path);
                if (date != null)
                    return (date, true);
            }

            // Videos
            if (FileScanner.VideoExtensions.Contains(ext))
            {
                var date = _videoMetadataService.GetCreationTime(path);
                if (date != null)
                    return (date, true);
            }

            // Documents
            if (FileScanner.DocumentExtensions.Contains(ext))
            {
                var date = _documentMetadataService.GetCreationTime(path);
                if (date != null)
                    return (date, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MEDIA DATE ERROR] {ex.Message}");
        }

        // ⚠️ LOW TRUST // ⚠️ LOW TRUST FALLBACK
        var fallback = GetSafeFileDate(path);

        if (fallback != null && fallback > DateTime.Now.AddDays(-30))
        {
            return (fallback, false);
        }

        return (null, false);
    }

    private static DateTime? TryParseDateFromFileName(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);

        var match = System.Text.RegularExpressions.Regex.Match(
            fileName,
            @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})[_ ](?<hour>\d{2})-(?<min>\d{2})-(?<sec>\d{2})"
        );

        if (match.Success)
        {
            return new DateTime(
                int.Parse(match.Groups["year"].Value),
                int.Parse(match.Groups["month"].Value),
                int.Parse(match.Groups["day"].Value),
                int.Parse(match.Groups["hour"].Value),
                int.Parse(match.Groups["min"].Value),
                int.Parse(match.Groups["sec"].Value)
            );
        }

        match = System.Text.RegularExpressions.Regex.Match(
            fileName,
            @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})"
        );

        if (match.Success)
        {
            return new DateTime(
                int.Parse(match.Groups["year"].Value),
                int.Parse(match.Groups["month"].Value),
                int.Parse(match.Groups["day"].Value)
            );
        }

        return null;
    }

    // ✅ FIXED fallback
    private DateTime? GetSafeFileDate(string path)
    {
        try
        {
            var info = new FileInfo(path);

            var created = info.CreationTime;
            var modified = info.LastWriteTime;

            // pick oldest = least manipulated
            return created < modified ? created : modified;
        }
        catch
        {
            return null;
        }
    }
}