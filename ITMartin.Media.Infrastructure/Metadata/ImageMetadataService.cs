using System.Globalization;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Xmp;
using SixLabors.ImageSharp;
namespace ITMartin.Media.Infrastructure.Metadata;

public class ImageMetadataService : IImageMetadataService
{

    public ImageMetadataService()
    {
    }

    public DateTime? GetCreationTime(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);

            var exifSubIfd = directories
                .OfType<ExifSubIfdDirectory>()
                .FirstOrDefault();

            var exifIfd0 = directories
                .OfType<ExifIfd0Directory>()
                .FirstOrDefault();

            DateTime? date = null;

            if (exifSubIfd != null)
            {
                if (exifSubIfd.TryGetDateTime(
                        ExifDirectoryBase.TagDateTimeOriginal,
                        out var originalDate))
                {
                    date = originalDate;
                }
                else if (exifSubIfd.TryGetDateTime(
                             ExifDirectoryBase.TagDateTimeDigitized,
                             out var digitizedDate))
                {
                    date = digitizedDate;
                }
            }

            if (date == null && exifIfd0 != null)
            {
                if (exifIfd0.TryGetDateTime(
                        ExifDirectoryBase.TagDateTime,
                        out var generalDate))
                {
                    date = generalDate;
                }
            }

            // Some HEIC-derived JPEGs (seen from certain iOS export paths)
            // carry no classic EXIF date tags at all - only Orientation - but
            // still have the real capture date in the XMP packet (which is
            // where Windows Explorer's "Date Taken" column reads it from in
            // that case). Worth trying before giving up on the image.
            if (date == null)
            {
                var xmp = directories.OfType<XmpDirectory>().FirstOrDefault();
                var xmpProps = xmp?.GetXmpProperties();
                if (xmpProps != null)
                {
                    foreach (var key in XmpDateKeys)
                    {
                        if (xmpProps.TryGetValue(key, out var raw) &&
                            TryParseXmpDate(raw, out var xmpDate))
                        {
                            date = xmpDate;
                            break;
                        }
                    }
                }
            }

            if (date != null)
            {
                return date;
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IMAGE METADATA ERROR] {ex.Message}");
        }

        return null;
    }

    // Priority order: the real capture moment, then whatever's next best.
    private static readonly string[] XmpDateKeys =
    [
        "exif:DateTimeOriginal",
        "photoshop:DateCreated",
        "xmp:CreateDate",
        "xmp:ModifyDate",
    ];

    private static bool TryParseXmpDate(string raw, out DateTime date)
    {
        // XMP dates are ISO 8601 ("2025-09-29T10:14:23+02:00") - Windows
        // Explorer's own "Date Taken" reader accepts the same format, which
        // is how these files show a real date there despite MetadataExtractor
        // finding nothing in the classic EXIF IFDs.
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public (int Width, int Height)? GetDimensions(
        string path)
    {
        try
        {
            var info =
                Image.Identify(path);

            if (info is null)
            {
                return null;
            }

            return (
                info.Width,
                info.Height);
        }
        catch
        {
            return null;
        }
    }

    public string? GetCameraModel(string path)
    {
        throw new NotImplementedException();
    }

    

    public string GetModelFromFileName(string path)
    {
        var fileName = Path.GetFileName(path).ToUpperInvariant();

        if (fileName.StartsWith("IMG_"))
            return "iPhone";

        return "Unknown";
    }
}