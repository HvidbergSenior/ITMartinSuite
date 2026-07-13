using System.Text.RegularExpressions;
using ITMartin.Media.Contracts.Contracts.Constants;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Infrastructure.Metadata;

public class MediaDateService : IMediaDateService
{
    private readonly IImageMetadataService _imageMetadataService;
    private readonly IVideoMetadataService _videoMetadataService;
    private readonly IDocumentMetadataService _documentMetadataService;

    public MediaDateService(
        IImageMetadataService imageMetadataService,
        IVideoMetadataService videoMetadataService,
        IDocumentMetadataService documentMetadataService)
    {
        _imageMetadataService = imageMetadataService;
        _videoMetadataService = videoMetadataService;
        _documentMetadataService = documentMetadataService;
    }

    public MediaDateResult GetBestDate(MediaDateRequest request)
    {
        // ✅ MANUAL OVERRIDE
        if (request.OverrideYear is not null)
        {
            return new MediaDateResult(
                new DateTime(request.OverrideYear.Value, 1, 1),
                false,
                "ManualOverride");
        }

        var path = request.Path;

        // ✅ 1. Filename (HIGH TRUST)
        var fileNameDate = TryParseDateFromFileName(path);

        if (fileNameDate != null)
        {
            Console.WriteLine($"[FILENAME DATE] {Path.GetFileName(path)} -> {fileNameDate}");

            return new MediaDateResult(
                fileNameDate,
                true,
                "Filename");
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();

        try
        {
            // Images
            if (MediaExtensions.ImageExtensions.Contains(ext))
            {
                var date = _imageMetadataService.GetCreationTime(path);

                if (date != null)
                {
                    return new MediaDateResult(
                        date,
                        true,
                        "ImageMetadata");
                }
            }

            // Videos
            if (MediaExtensions.VideoExtensions.Contains(ext))
            {
                var date = _videoMetadataService.GetCreationTime(path);

                if (date != null)
                {
                    return new MediaDateResult(
                        date,
                        true,
                        "VideoMetadata");
                }
            }

            // Documents
            if (MediaExtensions.DocumentExtensions.Contains(ext))
            {
                var date = _documentMetadataService.GetCreationTime(path);

                if (date != null)
                {
                    return new MediaDateResult(
                        date,
                        true,
                        "DocumentMetadata");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MEDIA DATE ERROR] {ex.Message}");
        }

        // ⚠️ LOW TRUST FALLBACK
        var fallback = GetSafeFileDate(path);

        if (fallback != null)
        {
            return new MediaDateResult(
                fallback,
                false,
                "Filesystem");
        }

        return new MediaDateResult(
            null,
            false,
            "None");
    }

    private static DateTime? TryParseDateFromFileName(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);

        var match = Regex.Match(
            fileName,
            @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})[_ ](?<hour>\d{2})-(?<min>\d{2})-(?<sec>\d{2})"
        );

        if (match.Success)
        {
            // Filenames matching the pattern aren't guaranteed to hold a real date
            // (e.g. a Dropbox export "2017-22-03 Referat..." has 22 in the month
            // position) - the constructor throws on out-of-range values instead of
            // returning false, and that used to crash the entire library scan.
            try
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
            catch (ArgumentOutOfRangeException)
            {
                // fall through to the next, looser pattern
            }
        }

        match = Regex.Match(
            fileName,
            @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})"
        );

        if (match.Success)
        {
            try
            {
                return new DateTime(
                    int.Parse(match.Groups["year"].Value),
                    int.Parse(match.Groups["month"].Value),
                    int.Parse(match.Groups["day"].Value)
                );
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return null;
    }

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