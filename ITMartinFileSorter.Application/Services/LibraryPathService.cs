using ITMartinFileSorter.Application.Helpers;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class LibraryPathService
{
    private readonly TripGroupingService _tripService;
    private readonly HomeLocationService _homeService;
    private readonly IGpsService _gpsService;

    public LibraryPathService(
        TripGroupingService tripService,
        HomeLocationService homeService,
        IGpsService gpsService)
    {
        _tripService = tripService;
        _homeService = homeService;
        _gpsService = gpsService;
    }

    public string BuildFolderPath(MediaFile file)
    {
        // User-defined folder always wins
        if (!string.IsNullOrWhiteSpace(file.UserFolderName))
        {
            var mainFolder = file.MainCategory switch
            {
                MediaMainCategory.Image => "Images",
                MediaMainCategory.Video => "Videos",
                MediaMainCategory.Audio => "Audio",
                MediaMainCategory.Document => "Documents",
                _ => "Other"
            };

            var year = GetYearFolder(file);

            return Path.Combine(
                mainFolder,
                year,
                CleanPart(file.UserFolderName));
        }

        // Existing automatic logic
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

        var main = file.MainCategory switch
        {
            MediaMainCategory.Image => "Images",
            MediaMainCategory.Video => "Videos",
            MediaMainCategory.Audio => "Audio",
            MediaMainCategory.Document => "Documents",
            _ => "Other"
        };

        var yearFolder = GetYearFolder(file);

        if (file.MainCategory is MediaMainCategory.Image
            or MediaMainCategory.Video)
        {
            var location = GetLocationFolder(file);
            var trip = GetTripName(file);

            return Path.Combine(main, yearFolder, location, trip);
        }

        return Path.Combine(main, yearFolder);
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

        if (!string.IsNullOrWhiteSpace(file.UserTitle))
        {
            parts.Add(CleanPart(file.UserTitle));
        }

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
        if (!string.IsNullOrWhiteSpace(file.Location) &&
            file.Location != "Unknown")
        {
            return CleanPart(file.Location);
        }

        var tripLocation = _tripService.GetSingleFileLocation(file);

        if (!string.IsNullOrWhiteSpace(tripLocation) &&
            tripLocation != "Unknown")
        {
            return CleanPart(tripLocation);
        }

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
    private string GetLocationFolder(MediaFile file)
    {
        if (file.MainCategory is not MediaMainCategory.Image
            and not MediaMainCategory.Video)
        {
            return "Unknown";
        }

        var coords = _gpsService.GetCoordinates(file.FullPath);

        if (coords == null)
            return "Unknown";

        var location = LocationFilter.GetLocationName(
            coords.Value.lat,
            coords.Value.lng);

        return string.IsNullOrWhiteSpace(location)
            ? "Unknown"
            : CleanPart(location);
    }
}