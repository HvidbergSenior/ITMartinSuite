using System.Globalization;
using System.Text.RegularExpressions;
using ITMartinFileSorter.Application.Helpers;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class TripGroupingService
{
    private readonly IGpsService _gpsService;

    public TripGroupingService(IGpsService gpsService)
    {
        _gpsService = gpsService;
    }

    public List<TripGroup> CreateTrips(IEnumerable<MediaFile> files)
    {
        var mediaFiles = files
            .Where(IsTripCandidate)
            .Where(f => f.CreatedAt != null)
            .OrderBy(f => f.CreatedAt)
            .ToList();

        var trips = new List<TripGroup>();

        if (!mediaFiles.Any())
            return trips;

        var currentTrip = new List<MediaFile>
        {
            mediaFiles.First()
        };

        for (int i = 1; i < mediaFiles.Count; i++)
        {
            var previous = mediaFiles[i - 1];
            var current = mediaFiles[i];

            var gap = current.CreatedAt!.Value - previous.CreatedAt!.Value;

            // ⚠️ OPTIONAL: Ignore unreliable dates for splitting
            if (!current.IsDateReliable || !previous.IsDateReliable)
            {
                currentTrip.Add(current);
                continue;
            }

            if (gap.TotalDays <= 7)
            {
                currentTrip.Add(current);
            }
            else
            {
                trips.Add(BuildTrip(currentTrip));
                currentTrip = new List<MediaFile> { current };
            }
        }

        if (currentTrip.Any())
            trips.Add(BuildTrip(currentTrip));

        return trips;
    }

    private bool IsTripCandidate(MediaFile file)
    {
        return file.MainCategory == MediaMainCategory.Image ||
               file.MainCategory == MediaMainCategory.Video;
    }

    private TripGroup BuildTrip(List<MediaFile> files)
    {
        var datedFiles = files
            .Where(f => f.CreatedAt != null)
            .Select(f => f.CreatedAt!.Value)
            .ToList();

        var start = datedFiles.Min();
        var end = datedFiles.Max();

        var location = GetTripLocation(files);

        return new TripGroup
        {
            Files = files,
            StartDate = start,
            EndDate = end,
            Name = BuildTripFolderName(start, end, location)
        };
    }

    private string BuildTripFolderName(
        DateTime start,
        DateTime end,
        string location)
    {
        var startMonth = GetMonth(start);
        var endMonth = GetMonth(end);

        if (start.Date == end.Date)
        {
            return start.ToString("yyyy-MM-dd");
        }

        string datePart;

        if (start.Month == end.Month)
        {
            datePart = $"{start.Day}-{end.Day}_{startMonth}";
        }
        else
        {
            datePart = $"{start.Day}_{startMonth}-{end.Day}_{endMonth}";
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            return $"{datePart}_{Clean(location)}";
        }

        return datePart;
    }

    private string GetMonth(DateTime date)
    {
        var culture = new CultureInfo("da-DK");
        var month = date.ToString("MMMM", culture);

        return Clean(char.ToUpper(month[0]) + month[1..]);
    }

    private string Clean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value
            .Replace("MediaLibrary", "")
            .Replace("Library", "")
            .Replace("Images", "")
            .Replace("Videos", "")
            .Replace(" ", "_")
            .Replace(".", "")
            .Replace(",", "")
            .Replace(":", "")
            .Replace("/", "_")
            .Replace("\\", "_")
            .Replace("__", "_")
            .Trim('_');
    }

    private string GetTripLocation(List<MediaFile> files)
    {
        Console.WriteLine("===== TRIP LOCATION DEBUG =====");

        foreach (var file in files)
        {
            var gpsLocation = GetGpsLocation(file);

            if (!string.IsNullOrWhiteSpace(gpsLocation) &&
                gpsLocation != "Unknown")
            {
                Console.WriteLine($"[TRIP LOCATION FROM GPS] {gpsLocation}");
                return gpsLocation;
            }
        }

        var first = files.FirstOrDefault();

        if (first != null)
        {
            var folder = Path.GetFileName(
                Path.GetDirectoryName(first.FullPath));

            var invalid = new[]
            {
                "MediaLibrary",
                "Library",
                "Images",
                "Videos",
                "DCIM"
            };

            if (invalid.Any(x =>
                    folder.Equals(x, StringComparison.OrdinalIgnoreCase)))
            {
                folder = "";
            }

            Console.WriteLine($"[FOLDER FALLBACK RAW] {folder}");

            var cleaned = Regex.Replace(
                folder ?? "",
                @"^\d{4}-\d{2}-\d{2}\s*",
                "")
                .Trim();

            Console.WriteLine($"[FOLDER FALLBACK CLEAN] {cleaned}");

            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return "Unknown";
    }

    private string GetGpsLocation(MediaFile file)
    {
        var coords = _gpsService.GetCoordinates(file.FullPath);

        Console.WriteLine("===== GPS DEBUG =====");
        Console.WriteLine($"[FILE] {file.FileName}");

        if (coords == null)
        {
            Console.WriteLine("[NO GPS FOUND]");
            return "Unknown";
        }

        var (lat, lng) = coords.Value;

        var location = LocationFilter.GetLocationName(lat, lng);

        Console.WriteLine($"[MATCH] {location}");

        return location;
    }

    public string GetSingleFileLocation(MediaFile file)
    {
        return GetGpsLocation(file);
    }
}