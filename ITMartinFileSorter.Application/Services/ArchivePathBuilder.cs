using ITMartinFileSorter.Application.Helpers;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Application.Services;

public class ArchivePathBuilder
{
    private readonly TripGroupingService _tripService;

    public ArchivePathBuilder(TripGroupingService tripService)
    {
        _tripService = tripService;
    }

    public Dictionary<string, List<MediaFile>> BuildStructure(
        IEnumerable<MediaFile> files,
        ArchiveOptions options)
    {
        var result = new Dictionary<string, List<MediaFile>>();

        var trips = options.UseTripFolders
            ? _tripService.CreateTrips(files)
            : new List<TripGroup>();

        foreach (var file in files)
        {
            var path = BuildPath(file, trips, options);

            if (!result.ContainsKey(path))
                result[path] = new List<MediaFile>();

            result[path].Add(file);
        }

        return result;
    }

    private string BuildPath(
        MediaFile file,
        List<TripGroup> trips,
        ArchiveOptions options)
    {
        var parts = new List<string>();
        switch (file.MainCategory)
        {
            case MediaMainCategory.Image:
                parts.Add("Photos");
                AddSubfolders(file, trips, options, parts);
                break;

            case MediaMainCategory.Video:
                parts.Add("Videos");
                AddSubfolders(file, trips, options, parts);
                break;

            case MediaMainCategory.Document:
                parts.Add("Documents");
                AddStandardSubfolders(file, options, parts);
                break;

            case MediaMainCategory.Audio:
                parts.Add("Audio");
                AddStandardSubfolders(file, options, parts);
                break;
        }

        return Path.Combine(parts.ToArray());
    }

    private void AddSubfolders(
        MediaFile file,
        List<TripGroup> trips,
        ArchiveOptions options,
        List<string> parts)
    {
        // TOP-LEVEL SPECIAL FOLDERS
        if (file.SubCategory == MediaSubCategory.Screenshot)
        {
            parts.Clear();
            parts.Add("Organized");
            parts.Add("Screenshots");
            return;
        }

        if (file.SubCategory == MediaSubCategory.Meme)
        {
            parts.Clear();
            parts.Add("Organized");
            parts.Add("Memes");
            return;
        }

        if (file.SubCategory == MediaSubCategory.Social)
        {
            parts.Clear();
            parts.Add("Organized");
            parts.Add("Social");
            return;
        }

        if (file.SubCategory == MediaSubCategory.ScreenRecording)
        {
            parts.Clear();
            parts.Add("Organized");
            parts.Add("ScreenRecordings");
            return;
        }

        // NORMAL IMAGE / VIDEO STRUCTURE
        AddStandardSubfolders(file, options, parts);

        var trip = trips.FirstOrDefault(t => t.Files.Contains(file));

        if (trip != null)
        {
            parts.Add(trip.Name);
            return;
        }

        if (!string.IsNullOrWhiteSpace(file.Location))
        {
            parts.Add(file.Location);
            return;
        }

        var location = _tripService.GetSingleFileLocation(file);

        if (!string.IsNullOrWhiteSpace(location) &&
            location != "Unknown")
        {
            parts.Add(location);
            return;
        }

        parts.Add("Mixed");
    }

    private void AddStandardSubfolders(
        MediaFile file,
        ArchiveOptions options,
        List<string> parts)
    {
        var bestDate = ImageMetadataHelper.GetBestDate(file.FullPath);

        var year = bestDate.Year;
        var month = bestDate.Month;

        if (options.UseYearFolders)
            parts.Add(year.ToString());

        if (options.UseMonthFolders)
        {
            var danishCulture = new System.Globalization.CultureInfo("da-DK");

            var monthName = new DateTime(year, month, 1)
                .ToString("MMMM", danishCulture);

            monthName = char.ToUpper(monthName[0]) + monthName[1..];

            // ONLY month name
            parts.Add(monthName);
        }

        if (options.UseTypeFolders)
            parts.Add(file.MainCategory.ToString());
    }
    
    private bool IsHomeLocation(string tripName)
    {
        return tripName.Contains("Aarhus", StringComparison.OrdinalIgnoreCase);
    }
}