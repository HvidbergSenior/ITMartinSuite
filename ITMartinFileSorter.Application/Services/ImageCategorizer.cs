using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

public class ImageCategorizer
{
    public void Categorize(MediaFile file)
    {
        var name = file.FileName.ToLowerInvariant();
        var ext = file.Extension.ToLowerInvariant();

        // 1️⃣ NAME-BASED DETECTION (always runs)
        if (IsScreenshot(name))
        {
            file.SubCategory = MediaSubCategory.Screenshot;
            return;
        }

        if (IsMeme(name))
        {
            file.SubCategory = MediaSubCategory.Meme;
            return;
        }

        if (IsSocial(name))
        {
            file.SubCategory = MediaSubCategory.Social;
            return;
        }

        if (IsScan(file, ext))
        {
            file.SubCategory = MediaSubCategory.OtherImage;
            return;
        }
        // 🔥 1.5 EXTENSION-BASED (VERY IMPORTANT)
        if (ext == ".gif")
        {
            file.SubCategory = MediaSubCategory.Meme;
            return;
        }
        
        // 2️⃣ DIMENSION-BASED DETECTION (if available)
        if (file.Width != null && file.Height != null)
        {
            var w = file.Width.Value;
            var h = file.Height.Value;

            // 📱 Screenshot (common resolutions)
            if ((w == 1920 && h == 1080) ||
                (w == 1080 && h == 1920) ||
                (w == 1170 && h == 2532) || // iPhone
                (w == 1284 && h == 2778))
            {
                file.SubCategory = MediaSubCategory.Screenshot;
                return;
            }

            // 😂 Meme / low quality
            if (w < 800 || h < 800)
            {
                file.SubCategory = MediaSubCategory.Meme;
                return;
            }

            // 📸 Real photo (high resolution)
            if (w >= 2000 && h >= 1500)
            {
                file.IsProbablyRealPhoto = true;
                file.SubCategory = MediaSubCategory.OtherImage;
                return;
            }
        }
// 🔥 If filename looks like phone screenshot but dimensions missing
        if (name.Contains("screen") || name.Contains("screenshot"))
        {
            file.SubCategory = MediaSubCategory.Screenshot;
            return;
        }
        // 3️⃣ FALLBACK (important!)
        file.SubCategory = MediaSubCategory.OtherImage;
    }

    private bool IsScreenshot(string name)
    {
        return name.Contains("screenshot") ||
               name.Contains("skærmbillede") ||
               name.Contains("screen shot") ||
               name.Contains("screen_") ||
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
}