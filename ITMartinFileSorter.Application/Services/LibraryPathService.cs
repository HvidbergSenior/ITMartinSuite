using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Application.Services;

public class LibraryPathService
{
    private readonly TripGroupingService _tripGroupingService;

    public LibraryPathService(
        TripGroupingService tripGroupingService)
    {
        _tripGroupingService = tripGroupingService;
    }

    public string BuildFolderPath(MediaFile file)
    {
        // Special top-level folders
        switch (file.SubCategory)
        {
            case MediaSubCategory.Screenshot:
                return Path.Combine(
                    "Screenshots",
                    GetYearFolder(file));

            case MediaSubCategory.Meme:
                return Path.Combine(
                    "Memes",
                    GetYearFolder(file));

            case MediaSubCategory.Social:
            case MediaSubCategory.WhatsApp:
            case MediaSubCategory.Telegram:
                return Path.Combine(
                    "Social",
                    GetYearFolder(file));
        }

        var mainFolder = file.MainCategory switch
        {
            MediaMainCategory.Image => "Images",
            MediaMainCategory.Video => "Videos",
            MediaMainCategory.Audio => "Audio",
            MediaMainCategory.Document => "Documents",
            _ => "Other"
        };

        var year = GetYearFolder(file);

        // Only images + videos should use trip folders
        if (file.MainCategory is MediaMainCategory.Image
            or MediaMainCategory.Video)
        {
            var trip = GetTripName(file);
            return Path.Combine(mainFolder, year, trip);
        }

        // Documents + audio should be flat
        return Path.Combine(mainFolder, year);
    }

    public string BuildFileName(MediaFile file, int index)
    {
        var date = file.CreatedAt?.ToString("yyyy-MM-dd_HH-mm-ss")
                   ?? "Unknown-Date";

        var location = CleanPart(file.Location);

        var trip = CleanPart(GetTripName(file));

        var category = GetCategoryLabel(file);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        var parts = new List<string>
        {
            date
        };

        if (!string.IsNullOrWhiteSpace(location))
            parts.Add(location);

        if (!string.IsNullOrWhiteSpace(trip))
            parts.Add(trip);

        parts.Add(category);
        parts.Add(index.ToString("D3"));

        return string.Join("_", parts) + ext;
    }

    private string GetTripName(MediaFile file)
    {
        // First use explicit file location if already resolved
        if (!string.IsNullOrWhiteSpace(file.Location) &&
            file.Location != "Unknown")
        {
            return CleanPart(file.Location);
        }

        // Fallback to trip grouping / GPS location service
        var tripLocation = _tripGroupingService.GetSingleFileLocation(file);

        if (!string.IsNullOrWhiteSpace(tripLocation) &&
            tripLocation != "Unknown")
        {
            return CleanPart(tripLocation);
        }

        // Fallback by date period
        if (file.CreatedAt != null)
        {
            return $"Trip_{file.CreatedAt:yyyy_MM}";
        }

        return "General";
    }

    private string GetCategoryLabel(MediaFile file)
    {
        return file.MainCategory switch
        {
            MediaMainCategory.Image => "Photo",
            MediaMainCategory.Video => "Video",
            MediaMainCategory.Audio => "Audio",
            MediaMainCategory.Document => "Document",
            _ => "File"
        };
    }

    private string CleanPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var cleaned = value
            .Replace(" ", "_")
            .Replace(",", "")
            .Replace(":", "")
            .Replace("/", "_")
            .Replace("\\", "_");

        return cleaned.Trim('_');
    }
    private string GetYearFolder(MediaFile file)
    {
        return file.CreatedAt?.Year.ToString() ?? "Unknown";
    }
}