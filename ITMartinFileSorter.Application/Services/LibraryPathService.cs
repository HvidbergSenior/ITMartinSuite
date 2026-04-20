using System.Globalization;
using ITMartinFileSorter.Application.Helpers;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class LibraryPathService
{
    private readonly IGpsService _gpsService;

    public LibraryPathService(IGpsService gpsService)
    {
        _gpsService = gpsService;
    }

    public string BuildFolderPath(MediaFile file)
    {
        var root = GetRootFolder(file);

        // 🚨 If NOT trustworthy → send to Other
        if (!IsTrustedMedia(file))
        {
            return Path.Combine(
                "Other",
                root,
                GetOtherSubFolder(file));
        }

        // ✅ Timeline structure ONLY for trusted media
        var year = GetYear(file);
        var month = GetMonthName(file);

        return Path.Combine(root, year, month);
    }

    public string BuildFileName(MediaFile file, int index)
    {
        var date = file.CreatedAt?.ToString("yyyy-MM-dd_HH-mm-ss")
                   ?? "Unknown_Date";

        var category = GetCategoryLabel(file);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        return $"{date}_{category}_{index:D3}{ext}";
    }

    // =========================
    // ROOT
    // =========================
    private string GetRootFolder(MediaFile file)
    {
        return file.MainCategory switch
        {
            MediaMainCategory.Image => "Images",
            MediaMainCategory.Video => "Videos",
            MediaMainCategory.Audio => "Audio",
            MediaMainCategory.Document => "Documents",
            _ => "Other"
        };
    }

    // =========================
    // TRUST LOGIC (VERY IMPORTANT)
    // =========================
    private bool IsTrustedMedia(MediaFile file)
    {
        if (file.CreatedAt == null)
            return false;

        if (file.CreatedAt.Value.Year < 1990)
            return false;

        // ✅ single source of truth
        return file.IsDateReliable;
    }

    // =========================
    // OTHER STRUCTURE
    // =========================
    private string GetOtherSubFolder(MediaFile file)
    {
        return file.SubCategory switch
        {
            // Images
            MediaSubCategory.Screenshot => "Screenshots",
            MediaSubCategory.Meme => "Memes",
            MediaSubCategory.Social => "Social",
            MediaSubCategory.OtherImage => "Other",

            // Videos
            MediaSubCategory.ScreenRecording => "ScreenRecordings",
            MediaSubCategory.OtherVideo => "Other",

            // Documents
            MediaSubCategory.Pdf => "Pdf",
            MediaSubCategory.Word => "Word",
            MediaSubCategory.Excel => "Excel",
            MediaSubCategory.Presentation => "Presentation",
            MediaSubCategory.Csv => "Csv",
            MediaSubCategory.UnknownDocument => "Other",

            // Audio
            MediaSubCategory.Music => "Music",
            MediaSubCategory.VoiceMemo => "Voice",
            MediaSubCategory.UnknownAudio => "Other",

            _ => "Unsorted"
        };
    }

    // =========================
    // DATE HELPERS
    // =========================
    private string GetYear(MediaFile file)
    {
        return file.CreatedAt?.Year.ToString() ?? "Unknown";
    }

    private string GetMonthName(MediaFile file)
    {
        if (file.CreatedAt == null)
            return "Unknown";

        var culture = new CultureInfo("da-DK");

        var month = file.CreatedAt.Value
            .ToString("MMMM", culture);

        // Capitalize + remove spaces
        return Capitalize(month);
    }

    private string Capitalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return char.ToUpper(value[0]) + value[1..];
    }

    // =========================
    // FILE LABEL
    // =========================
    private string GetCategoryLabel(MediaFile file)
    {
        return file.MainCategory switch
        {
            MediaMainCategory.Image => "Photo",
            MediaMainCategory.Video => "Video",
            MediaMainCategory.Audio => "Audio",
            MediaMainCategory.Document => "Doc",
            _ => "File"
        };
    }
}