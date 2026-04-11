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

        if (IsScreenshot(file, name, ext))
        {
            file.SubCategory = MediaSubCategory.Screenshot;
        }
        else if (IsMeme(file, name, ext))
        {
            file.SubCategory = MediaSubCategory.Meme;
        }
        else if (IsSocial(file, name))
        {
            file.SubCategory = MediaSubCategory.Social;
        }
        else if (IsCameraPhoto(name, ext))
        {
            file.SubCategory = MediaSubCategory.Camera;
        }
        else
        {
            file.SubCategory = MediaSubCategory.OtherImage;
        }

        // NEW EXPORT STRUCTURE
        file.DynamicFolder = file.SubCategory switch
        {
            MediaSubCategory.Screenshot => "Screenshots",
            MediaSubCategory.Meme => "Memes",
            _ => Path.Combine("Images", yearMonth)
        };
    }

    private bool IsScreenshot(MediaFile file, string name, string ext)
    {
        if (ext == ".png")
            return true;

        return name.Contains("screenshot") ||
               name.Contains("screen") ||
               name.Contains("capture");
    }

    private bool IsMeme(MediaFile file, string name, string ext)
    {
        if (ext == ".gif" || ext == ".webp")
            return true;

        return name.Contains("meme") ||
               name.Contains("funny") ||
               name.Contains("reaction") ||
               name.Contains("sticker") ||
               name.Contains("joker");
    }

    private bool IsSocial(MediaFile file, string name)
    {
        return name.Contains("facebook") ||
               name.Contains("messenger") ||
               name.Contains("whatsapp") ||
               name.Contains("instagram") ||
               name.Contains("snapchat");
    }

    private bool IsCameraPhoto(string name, string ext)
    {
        if (ext == ".heic" ||
            ext == ".jpg" ||
            ext == ".jpeg")
            return true;

        return name.StartsWith("img_") ||
               name.StartsWith("pxl_") ||
               name.StartsWith("dsc_");
    }
}