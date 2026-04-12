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
    private readonly IMediaDateService _mediaDateService;

    public TripGroupingService(
        IGpsService gpsService,
        IMediaDateService mediaDateService)
    {
        _gpsService = gpsService;
        _mediaDateService = mediaDateService;
    }

    public List<TripGroup> CreateTrips(IEnumerable<MediaFile> files)
    {
        var mediaFiles = files
            .Where(IsTripCandidate)
            .Select(f => new
            {
                File = f,
                Date = _mediaDateService.GetBestDate(f.FullPath)
            })
            .Where(x => x.Date != null)
            .OrderBy(x => x.Date)
            .ToList();

        var trips = new List<TripGroup>();

        if (!mediaFiles.Any())
            return trips;

        var currentTrip = new List<MediaFile>
        {
            mediaFiles.First().File
        };

        for (int i = 1; i < mediaFiles.Count; i++)
        {
            var previous = mediaFiles[i - 1];
            var current = mediaFiles[i];

            var gap = current.Date!.Value - previous.Date!.Value;

            if (gap.TotalDays <= 7)
            {
                currentTrip.Add(current.File);
            }
            else
            {
                trips.Add(BuildTrip(currentTrip));
                currentTrip = new List<MediaFile> { current.File };
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
            .Select(f => _mediaDateService.GetBestDate(f.FullPath))
            .Where(d => d != null)
            .Select(d => d!.Value)
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
        var culture = new CultureInfo("da-DK");

        var startMonth = start.ToString("MMMM", culture);
        var endMonth = end.ToString("MMMM", culture);

        startMonth =
            char.ToUpper(startMonth[0]) +
            startMonth[1..];

        endMonth =
            char.ToUpper(endMonth[0]) +
            endMonth[1..];

        var datePart =
            $"{start.Day}{startMonth}-{end.Day}{endMonth}";

        if (!string.IsNullOrWhiteSpace(location) &&
            location != "Unknown")
        {
            return $"{datePart} {location}";
        }

        return datePart;
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
                Console.WriteLine(
                    $"[TRIP LOCATION FROM GPS] {gpsLocation}");

                return gpsLocation;
            }
        }

        var first = files.FirstOrDefault();

        if (first != null)
        {
            var folder = Path.GetFileName(
                Path.GetDirectoryName(first.FullPath));

            Console.WriteLine(
                $"[FOLDER FALLBACK RAW] {folder}");

            var cleaned = Regex.Replace(
                folder ?? "",
                @"^\d{4}-\d{2}-\d{2}\s*",
                "")
                .Trim();

            Console.WriteLine(
                $"[FOLDER FALLBACK CLEAN] {cleaned}");

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
        Console.WriteLine($"[PATH] {file.FullPath}");

        if (coords == null)
        {
            Console.WriteLine("[NO GPS FOUND]");
            return "Unknown";
        }

        var (lat, lng) = coords.Value;

        Console.WriteLine($"[LAT] {lat}");
        Console.WriteLine($"[LNG] {lng}");

        var location = LocationFilter.GetLocationName(lat, lng);

        Console.WriteLine($"[MATCH] {location}");

        return location;
    }

    public string GetSingleFileLocation(MediaFile file)
    {
        return GetGpsLocation(file);
    }
}