using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;

namespace ITMartinFileSorter.Infrastructure.Helpers;

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
    public static (int? Width, int? Height) GetDimensions(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);

            // ✅ 1. EXIF (most reliable for camera images)
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            if (subIfd != null &&
                subIfd.TryGetInt32(ExifDirectoryBase.TagExifImageWidth, out var exifWidth) &&
                subIfd.TryGetInt32(ExifDirectoryBase.TagExifImageHeight, out var exifHeight))
            {
                return (exifWidth, exifHeight);
            }

            // ✅ 2. JPEG fallback
            var jpeg = directories.OfType<JpegDirectory>().FirstOrDefault();

            if (jpeg != null &&
                jpeg.TryGetInt32(JpegDirectory.TagImageWidth, out var jpegWidth) &&
                jpeg.TryGetInt32(JpegDirectory.TagImageHeight, out var jpegHeight))
            {
                return (jpegWidth, jpegHeight);
            }

            // ✅ 3. Generic fallback (PNG, WebP, etc.)
            var dirWithDims = directories.FirstOrDefault(d =>
                d.ContainsTag(ExifDirectoryBase.TagImageWidth) &&
                d.ContainsTag(ExifDirectoryBase.TagImageHeight));

            if (dirWithDims != null &&
                dirWithDims.TryGetInt32(ExifDirectoryBase.TagImageWidth, out var w) &&
                dirWithDims.TryGetInt32(ExifDirectoryBase.TagImageHeight, out var h))
            {
                return (w, h);
            }
        }
        catch
        {
            // ignore
        }

        return (null, null);
    }
}