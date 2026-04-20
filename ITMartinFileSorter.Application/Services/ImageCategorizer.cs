using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Application.Services;

public class ImageCategorizer
{
    public void Categorize(MediaFile file)
    {
        var name = file.FileName.ToLowerInvariant();
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        string yearMonth = file.Year > 1990
            ? $"{file.Year}-{file.Month:00}"
            : "Unknown";

        // 🔥 ORDER MATTERS (most specific first)

        if (IsScreenshot(name))
        {
            file.SubCategory = MediaSubCategory.Screenshot;
        }
        else if (IsMeme(name))
        {
            file.SubCategory = MediaSubCategory.Meme;
        }
        else if (IsSocial(name))
        {
            file.SubCategory = MediaSubCategory.Social;
        }
        else if (IsScan(file, ext))
        {
            file.SubCategory = MediaSubCategory.OtherImage; // or create Scan enum later
        }
        else if (IsCameraPhoto(name, ext))
        {
            file.SubCategory = MediaSubCategory.Camera;
        }
        else
        {
            file.SubCategory = MediaSubCategory.OtherImage;
        }

        
    }

    private bool IsScreenshot(string name)
    {
        return name.Contains("screenshot") ||
               name.Contains("skærmbillede") ||
               name.Contains("screen") ||
               name.Contains("capture") ||
               name.StartsWith("snip");
    }

    private bool IsMeme(string name)
    {
        return name.Contains("meme") ||
               name.Contains("funny") ||
               name.Contains("reaction") ||
               name.Contains("sticker") ||
               name.Contains("joker");
    }

    private bool IsSocial(string name)
    {
        return name.Contains("facebook") ||
               name.Contains("messenger") ||
               name.Contains("whatsapp") ||
               name.Contains("instagram") ||
               name.Contains("snapchat");
    }

    private bool IsScan(MediaFile file, string ext)
    {
        return ext is ".tif" or ".tiff"
               || file.SizeBytes > 10_000_000;
    }

    private bool IsCameraPhoto(string name, string ext)
    {
        // common camera formats
        if (ext is ".jpg" or ".jpeg" or ".heic" or ".png")
            return true;

        return name.StartsWith("img_") ||
               name.StartsWith("pxl_") ||
               name.StartsWith("dsc_");
    }
}