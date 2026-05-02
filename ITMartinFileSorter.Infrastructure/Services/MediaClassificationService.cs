using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Infrastructure.Services;

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

        // ---------- SCREENSHOT ----------
        if (IsScreenshotByName(name) || IsScreenshotBySize(file))
        {
            file.SubCategory = MediaSubCategory.Screenshot;
            return;
        }

        // ---------- PHONE PHOTO ----------
        if (name.StartsWith("img_") || file.HasExif)
        {
            file.SubCategory = MediaSubCategory.PhonePhoto;
            return;
        }

        // ---------- MEME ----------
        if (name.Contains("meme") || name.Contains("funny"))
        {
            file.SubCategory = MediaSubCategory.OtherImage; // keep SubCategory clean
            file.Tags.Add("meme");
            return;
        }

        // ---------- DEFAULT ----------
        file.SubCategory = MediaSubCategory.OtherImage;
    }

    private static bool IsScreenshotByName(string name)
    {
        return name.Contains("screenshot") ||
               name.Contains("screen_shot") ||
               name.StartsWith("screencapture");
    }

    private static bool IsScreenshotBySize(MediaFile file)
    {
        if (!file.Width.HasValue || !file.Height.HasValue)
            return false;

        var ratio = (double)file.Width / file.Height;

        return ratio is > 0.4 and < 0.6; // phone portrait screenshots
    }

    // =========================
    // VIDEO
    // =========================
    private void ClassifyVideo(MediaFile file)
    {
        var name = file.FileName.ToLowerInvariant();

        file.Source = DetectSource(file);

        if (name.Contains("screenrecord"))
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