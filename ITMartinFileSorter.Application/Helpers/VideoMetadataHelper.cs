using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ITMartinFileSorter.Application.Helpers;

public static class VideoMetadataHelper
{
    public static (string? Model, DateTime? Created)? ReadMetadata(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);

            var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var created = exif?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);

            DateTime? createdAt = null;
            if (DateTime.TryParse(created, out var dt))
                createdAt = dt;

            return (Model: null, Created: createdAt);
        }
        catch
        {
            return null;
        }
    }

    public static DateTime? GetCreationTime(string path)
    {
        return ReadMetadata(path)?.Created;
    }

    public static string GetModelFromFileName(string path)
    {
        string fileName = Path.GetFileName(path).ToUpper();

        if (fileName.StartsWith("IMG_") || fileName.StartsWith("VID_"))
            return "iPhone";

        return "Unknown";
    }
}