using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ITMartinFileSorter.Application.Helpers;

public static class ImageMetadataHelper
{
    public static DateTime? GetCreationTime(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);

            var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exif != null && exif.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out var date))
            {
                return date;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
    public static DateTime GetBestDate(string path)
    {
        // 1. Try EXIF
        var exifDate = GetCreationTime(path);
        if (exifDate != null)
            return exifDate.Value;

        // 2. Fallback to file system (NOT creation time)
        var modified = File.GetLastWriteTime(path);

        if (modified.Year > 2000)
            return modified;

        // 3. Last fallback
        return DateTime.Now;
    }
}