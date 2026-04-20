using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Application.Services;

public class ImageCategorizer
{
    public void Categorize(MediaFile file)
    {
        DetectRealPhoto(file);
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
    private void DetectRealPhoto(MediaFile file)
    {
        if (file.Width == null || file.Height == null)
            return;

        // ✅ Real camera photo (even converted HEIC)
        if (file.Width >= 2000 && file.Height >= 1500)
        {
            file.IsProbablyRealPhoto = true;
            return;
        }

        // ✅ Screenshot detection
        var isScreenshot =
            (file.Width == 1920 && file.Height == 1080) ||
            (file.Width == 1080 && file.Height == 1920);

        if (isScreenshot)
        {
            file.SubCategory = MediaSubCategory.Screenshot;
            return;
        }

        // ✅ Meme / low quality
        if (file.Width < 800 || file.Height < 800)
        {
            file.SubCategory = MediaSubCategory.Meme;
            return;
        }
    }
}