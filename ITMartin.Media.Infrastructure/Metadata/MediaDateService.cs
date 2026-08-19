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

        // 📁 YEAR-ONLY, FROM AN ANCESTOR FOLDER NAME
        // Real customer archives are routinely pre-organized into year
        // folders by whatever tool/person handled them before FileSorter -
        // often the *only* real signal left once a video's been re-encoded
        // by that earlier pass and lost its embedded creation_time. Ahead of
        // the raw filesystem timestamp below: a human-assigned year folder
        // is more informative than "whenever this happened to be copied".
        // Still not a full date - Month/Day are unknown, so this always
        // carries IsYearOnly=true and IsReliable=false; export routing sends
        // it to "{year}/Ukendt måned", never a specific month.
        var parentYear = TryInferYearFromParentFolder(path);

        if (parentYear != null)
        {
            return new MediaDateResult(
                new DateTime(parentYear.Value, 1, 1),
                false,
                "ParentFolderYear",
                IsYearOnly: true);
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

    // Walks up from the file's own folder looking for an ancestor whose name
    // is (or contains) a standalone plausible year - e.g. ".../Exported/
    // Video/2010/MVI_1473.mp4" -> 2010. Capped at a few levels so an
    // unrelated distant ancestor (a drive letter, a customer name) can't
    // accidentally match. Nearest folder wins; a folder matching literally
    // "today's year" is not excluded - it's exactly as untrustworthy as any
    // other single-signal year folder, which is why this tier is IsYearOnly/
    // IsReliable=false rather than a full trusted date either way.
    private static readonly Regex YearFolderPattern = new(@"(?<![0-9])(19[5-9]\d|20\d{2})(?![0-9])");
    private const int MaxAncestorLevels = 5;

    private static int? TryInferYearFromParentFolder(string path)
    {
        var dir = Path.GetDirectoryName(path);
        var levels = 0;

        while (!string.IsNullOrEmpty(dir) && levels < MaxAncestorLevels)
        {
            var name = Path.GetFileName(dir);
            var match = YearFolderPattern.Match(name);

            if (match.Success && int.TryParse(match.Value, out var year) && year <= DateTime.Now.Year)
                return year;

            dir = Path.GetDirectoryName(dir);
            levels++;
        }

        return null;
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