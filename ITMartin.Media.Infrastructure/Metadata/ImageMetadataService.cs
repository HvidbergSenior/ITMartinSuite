using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
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