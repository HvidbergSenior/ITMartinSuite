using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ITMartinFileSorter.Application.Helpers;

public static class ExifHelper
{
    public static (string? Make, string? Model, string? Software)? ReadMetadata(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);

            var exif = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            string? make = exif?.GetDescription(ExifDirectoryBase.TagMake);
            string? model = exif?.GetDescription(ExifDirectoryBase.TagModel);
            string? software = directories
                .SelectMany(d => d.Tags)
                .FirstOrDefault(t => t.Name.Contains("Software", StringComparison.OrdinalIgnoreCase))
                ?.Description;

            return (make, model, software);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsIphone(string path)
    {
        var meta = ReadMetadata(path);
        if (meta == null) return false;

        return meta.Value.Make?.Contains("Apple", StringComparison.OrdinalIgnoreCase) == true ||
               meta.Value.Model?.Contains("iPhone", StringComparison.OrdinalIgnoreCase) == true ||
               meta.Value.Software?.Contains("Apple", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static bool IsAndroid(string path)
    {
        var meta = ReadMetadata(path);
        if (meta == null) return false;

        return meta.Value.Make != null &&
               !meta.Value.Make.Contains("Apple", StringComparison.OrdinalIgnoreCase);
    }
}