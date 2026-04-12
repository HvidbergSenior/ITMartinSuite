using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ITMartinFileSorter.Infrastructure.Helpers;

public static class ImageMetadataHelper
{
    public static DateTime? GetCreationTime(string path)
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

            if (date != null)
            {
                Console.WriteLine($"[IMAGE DATE] {Path.GetFileName(path)} -> {date}");
                return date;
            }

            Console.WriteLine($"[IMAGE DATE NOT FOUND] {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IMAGE METADATA ERROR] {ex.Message}");
        }

        return null;
    }

    public static string GetModelFromFileName(string path)
    {
        var fileName = Path.GetFileName(path).ToUpperInvariant();

        if (fileName.StartsWith("IMG_"))
            return "iPhone";

        return "Unknown";
    }
}