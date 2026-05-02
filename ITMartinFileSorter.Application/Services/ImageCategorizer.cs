using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class ImageCategorizer : IMediaSubCategorizer
{
    public MediaType Type => MediaType.Image;
    public void Categorize(MediaFile file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        if (file.Type != MediaType.Image)
            throw new InvalidOperationException(
                $"Invalid media type: {file.Type}. Expected Image.");

        var name = file.FileName.ToLowerInvariant();
        var ext = file.Extension.ToLowerInvariant();

        // 1️⃣ NAME-BASED (highest priority)
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

        // 2️⃣ EXTENSION RULES
        if (ext == ".gif")
        {
            file.SubCategory = MediaSubCategory.Meme;
            return;
        }

        // 3️⃣ DIMENSION RULES
        if (file.Width is int w && file.Height is int h)
        {
            if (IsScreenshotResolution(w, h))
            {
                file.SubCategory = MediaSubCategory.Screenshot;
                return;
            }

            if (IsLowResolution(w, h))
            {
                file.SubCategory = MediaSubCategory.Meme;
                return;
            }

            if (IsHighResolution(w, h))
            {
                file.IsProbablyRealPhoto = true;
                file.SubCategory = MediaSubCategory.OtherImage;
                return;
            }
        }

        // 4️⃣ FALLBACK NAME CHECK
        if (name.Contains("screen"))
        {
            file.SubCategory = MediaSubCategory.Screenshot;
            return;
        }

        // 5️⃣ DEFAULT
        file.SubCategory = MediaSubCategory.OtherImage;
    }

    // ===== RULES =====

    private static bool IsScreenshot(string name) =>
        name.Contains("screenshot") ||
        name.Contains("skærmbillede") ||
        name.Contains("screen shot") ||
        name.Contains("screen_") ||
        name.Contains("capture") ||
        name.StartsWith("snip");

    private static bool IsMeme(string name) =>
        name.Contains("meme") ||
        name.Contains("funny") ||
        name.Contains("reaction") ||
        name.Contains("sticker");

    private static bool IsSocial(string name) =>
        name.Contains("facebook") ||
        name.Contains("messenger") ||
        name.Contains("whatsapp") ||
        name.Contains("instagram") ||
        name.Contains("snapchat");

    private static bool IsScreenshotResolution(int w, int h) =>
        (w == 1920 && h == 1080) ||
        (w == 1080 && h == 1920) ||
        (w == 1170 && h == 2532) ||
        (w == 1284 && h == 2778);

    private static bool IsLowResolution(int w, int h) =>
        w < 800 || h < 800;

    private static bool IsHighResolution(int w, int h) =>
        w >= 2000 && h >= 1500;
}