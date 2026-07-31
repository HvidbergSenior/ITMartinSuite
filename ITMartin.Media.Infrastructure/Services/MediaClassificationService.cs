using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Infrastructure.Services;

public class MediaClassificationService : IMediaClassificationService
{
    public void Classify(MediaFile file)
    {
        switch (file.Type)
        {
            case MediaType.Image:
                ClassifyImage(file);
                break;

            case MediaType.Video:
                ClassifyVideo(file);
                break;

            case MediaType.Audio:
                ClassifyAudio(file);
                break;

            case MediaType.Document:
                ClassifyDocument(file);
                break;
        }
    }

    // =========================
    // IMAGE
    // =========================
    private void ClassifyImage(MediaFile file)
    {
        var name = file.FileName.ToLowerInvariant();

        // ---------- SOURCE ----------
        file.Source = DetectSource(file);

        // ---------- PHONE PHOTO (checked before SCREENSHOT) ----------
        // A camera-style filename or real EXIF camera metadata is decisive
        // proof this is a real photo, not a screenshot - has to be checked
        // before the size-based screenshot heuristic below, which otherwise
        // has no way to tell a real PNG photo that happens to match a phone's
        // screen resolution (confirmed in production: PNG-exported photos at
        // 1170x2532 named "IMG 1234.png" were misfiled as screenshots) from an
        // actual screenshot. "img " (space) is included alongside "img_"
        // (underscore) since some export/sync tools normalize the underscore
        // to a space.
        var isCameraNamed = name.StartsWith("img_") || name.StartsWith("img ");
        if (isCameraNamed || file.HasExif)
        {
            // An explicit screenshot filename still wins even if it also
            // happens to start with "img" or carry stray EXIF.
            if (IsScreenshotByName(name))
            {
                file.SubCategory = MediaSubCategory.Screenshot;
                return;
            }

            file.SubCategory = MediaSubCategory.PhonePhoto;
            return;
        }

        // ---------- SCREENSHOT ----------
        // Size-only matching is restricted to PNG: real screenshot tools save
        // lossless PNG, so a JPG at the exact same pixel dimensions as a phone
        // screen is almost always a real (often message-compressed) photo
        // that happens to share that resolution, not an actual screenshot.
        if (IsScreenshotByName(name) || (name.EndsWith(".png") && IsScreenshotBySize(file)))
        {
            file.SubCategory = MediaSubCategory.Screenshot;
            return;
        }

        // ---------- MEME ----------
        if (IsMeme(file, name))
        {
            file.SubCategory = MediaSubCategory.Meme;
            return;
        }

        // ---------- DEFAULT ----------
        file.SubCategory = MediaSubCategory.OtherImage;
    }

    private static bool IsScreenshotByName(string name)
    {
        return name.Contains("screenshot") ||
               name.Contains("screen_shot") ||
               name.Contains("screen shot") ||
               name.StartsWith("screencapture") ||
               name.StartsWith("capture");
    }

    private static bool IsScreenshotBySize(MediaFile file)
    {
        if (!file.Width.HasValue || !file.Height.HasValue)
            return false;

        var w = file.Width!.Value;
        var h = file.Height!.Value;

        // Common screen resolutions — landscape and portrait
        return (w, h) switch
        {
            (1920, 1080) or (1080, 1920) => true,
            (2560, 1440) or (1440, 2560) => true,
            (2560, 1600) or (1600, 2560) => true,
            (3840, 2160) or (2160, 3840) => true,
            (1280, 800)  or (800, 1280)  => true,
            (1366, 768)  or (768, 1366)  => true,
            (2732, 2048) or (2048, 2732) => true, // iPad
            (1170, 2532) or (2532, 1170) => true, // iPhone 12/13
            (1284, 2778) or (2778, 1284) => true, // iPhone Pro Max
            (1080, 2340) or (2340, 1080) => true, // Android common
            (1080, 2400) or (2400, 1080) => true,
            // Older/smaller iPhones - real gap that let a genuine lock-screen
            // screenshot (640x960) sit misclassified as a real photo in Images.
            (640, 960)   or (960, 640)   => true, // iPhone 4/4s
            (640, 1136)  or (1136, 640)  => true, // iPhone 5/5s/SE (1st gen)
            (750, 1334)  or (1334, 750)  => true, // iPhone 6/7/8/SE (2nd/3rd gen)
            (1242, 2208) or (2208, 1242) => true, // iPhone 6+/7+/8+
            (1125, 2436) or (2436, 1125) => true, // iPhone X/XS/11 Pro
            (828, 1792)  or (1792, 828)  => true, // iPhone XR/11
            (1242, 2688) or (2688, 1242) => true, // iPhone XS Max/11 Pro Max
            _ => false
        };
    }

    private static bool IsMeme(MediaFile file, string name)
    {
        if (name.Contains("meme") || name.Contains("funny"))
            return true;

        var path = file.FullPath.ToLowerInvariant();
        if (path.Contains("meme") || path.Contains("9gag") ||
            path.Contains("ifunny") || path.Contains("reddit"))
            return true;

        return false;
    }

    // =========================
    // VIDEO
    // =========================
    private void ClassifyVideo(MediaFile file)
    {
        var name = file.FileName.ToLowerInvariant();

        file.Source = DetectSource(file);

        // "screenrecord" alone misses iOS's real native filename (RPReplay_Final...)
        // and "Screen Recording ..." (with a space) - both real, common naming
        // conventions this was previously silently missing.
        if (name.Contains("screenrecord") ||
            name.Contains("screen record") ||
            name.StartsWith("rpreplay"))
        {
            file.SubCategory = MediaSubCategory.ScreenRecording;
            return;
        }

        if (name.StartsWith("vid_") || file.HasExif)
        {
            file.SubCategory = MediaSubCategory.PhoneVideo;
            return;
        }

        file.SubCategory = MediaSubCategory.OtherVideo;
    }

    // =========================
    // AUDIO
    // =========================
    private void ClassifyAudio(MediaFile file)
    {
        var name = file.FileName.ToLowerInvariant();

        if (name.Contains("voice") || name.Contains("record"))
        {
            file.SubCategory = MediaSubCategory.VoiceMemo;
            return;
        }

        file.SubCategory = MediaSubCategory.Music;
    }

    // =========================
    // DOCUMENT
    // =========================
    private void ClassifyDocument(MediaFile file)
    {
        var ext = file.Extension.ToLowerInvariant();

        file.SubCategory = ext switch
        {
            ".pdf" => MediaSubCategory.Pdf,
            ".doc" or ".docx" => MediaSubCategory.Word,
            ".xls" or ".xlsx" => MediaSubCategory.Excel,
            ".ppt" or ".pptx" => MediaSubCategory.Presentation,
            ".csv" => MediaSubCategory.Csv,
            ".txt" => MediaSubCategory.Text,
            _ => MediaSubCategory.UnknownDocument
        };
    }

    // =========================
    // SOURCE DETECTION
    // =========================
    private static MediaSource DetectSource(MediaFile file)
    {
        var path = file.FullPath.ToLowerInvariant();

        if (path.Contains("whatsapp"))
            return MediaSource.WhatsApp;

        if (path.Contains("telegram"))
            return MediaSource.Telegram;

        if (path.Contains("download"))
            return MediaSource.Download;

        return MediaSource.Unknown;
    }
}