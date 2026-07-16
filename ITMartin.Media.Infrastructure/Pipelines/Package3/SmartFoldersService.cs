using System.Net;
using System.Text;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;
using ITMartin.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.Package3;

public sealed class SmartFoldersService : ISmartFoldersService
{
    public const string RootFolderName = "SmartFolders";

    // Shared "how far from home counts as away" threshold for both the
    // Home/Outside split and trip clustering.
    private const double AwayFromHomeKm = 10;
    private const double HomeBucketDegrees = 0.05; // roughly 5km grid cells for finding "home"

    private static readonly HashSet<string> SkippedFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "@eadir", "#recycle", "#snapshot", ".@__thumb",
            "@recently-snapshot", ".synophoto", ".package1", ".package2", ".package3",
            "thumbnails", "working", "enhanced", "manifests", "temp", RootFolderName,
            // Same reasoning as Package3Service - motion-clip companions to
            // already-counted stills, not standalone content.
            "LivePhotos",
            // Generated offline-gallery thumbnails - not real content.
            "_Galleri",
        };

    // Coarse, offline country lookup for trip-folder naming - deliberately rough
    // (rectangular bounding boxes, some neighbour overlap) rather than a paid
    // geocoding API. Good enough for "which country was this roughly in", not
    // meant to be precise at borders.
    private static readonly (string Name, double MinLat, double MaxLat, double MinLng, double MaxLng)[] CountryBoxes =
    [
        ("Danmark", 54.5, 57.8, 8.0, 15.2),
        ("Italien", 36.6, 47.1, 6.6, 18.6),
        ("Spanien", 27.6, 43.8, -18.2, 4.4),
        ("Frankrig", 41.3, 51.1, -5.2, 9.6),
        ("Tyskland", 47.2, 55.1, 5.8, 15.1),
        ("Sverige", 55.3, 69.1, 11.0, 24.2),
        ("Norge", 57.9, 71.2, 4.5, 31.3),
        ("Grækenland", 34.8, 41.8, 19.3, 29.7),
        ("Kroatien", 42.3, 46.6, 13.4, 19.5),
        ("Portugal", 36.9, 42.2, -9.6, -6.1),
        ("Østrig", 46.3, 49.1, 9.5, 17.2),
        ("Holland", 50.7, 53.6, 3.3, 7.3),
        ("Storbritannien", 49.8, 60.9, -8.7, 1.8),
        ("Tyrkiet", 35.8, 42.2, 25.6, 44.8),
        ("Thailand", 5.6, 20.5, 97.3, 105.7),
        ("USA", 24.4, 49.4, -125.0, -66.9),
    ];

    private readonly IDbContextFactory<MediaDbContext> _dbFactory;
    private readonly IPackage3Service _package3;
    private readonly IGpsService _gps;
    private readonly IMediaDateService _dateService;
    private readonly ICollectionStore _collectionStore;
    private readonly ILogger<SmartFoldersService> _logger;

    public SmartFoldersService(
        IDbContextFactory<MediaDbContext> dbFactory,
        IPackage3Service package3,
        IGpsService gps,
        IMediaDateService dateService,
        ICollectionStore collectionStore,
        ILogger<SmartFoldersService> logger)
    {
        _dbFactory = dbFactory;
        _package3 = package3;
        _gps = gps;
        _dateService = dateService;
        _collectionStore = collectionStore;
        _logger = logger;
    }

    public async Task<PersonFolderResult?> GeneratePersonFolderAsync(string libraryPath, Guid personId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.People.FindAsync([personId], cancellationToken);
        if (person is null) return null;

        var matches = await _package3.FindMatchesAsync(personId);
        var filePaths = matches.Select(m => m.MediaFilePath).ToList();
        if (filePaths.Count == 0) return null;

        var folderPath = Path.Combine(libraryPath, RootFolderName, "People", SanitizeName(person.Name));
        var linked = LinkFiles(filePaths, folderPath);

        _logger.LogInformation("Generated person folder for {Name}: {Linked}/{Matched} files linked at {Path}", person.Name, linked.Count, filePaths.Count, folderPath);

        return new PersonFolderResult { Name = person.Name, FileCount = linked.Count, FolderPath = folderPath };
    }

    private List<(string Path, DateTime Date, double? Lat, double? Lng)> GatherDateAndGpsPoints(
        string libraryPath, CancellationToken cancellationToken)
    {
        // Images only - same reasoning as GenerateYearbookAsync: video date
        // extraction shells out to ffprobe per file, turning this into a
        // multi-hour scan on a large, video-heavy library for what should be
        // a quick Home/Away or Trip pass. GPS-tagged videos are rare enough
        // that Trips/Home-Away built from photos alone are still meaningful.
        var files = EnumerateLibraryImages(libraryPath).Where(MediaTypeHelper.IsImage).ToList();
        var points = new List<(string Path, DateTime Date, double? Lat, double? Lng)>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dateResult = _dateService.GetBestDate(new MediaDateRequest(file));
            if (dateResult.Date is null) continue;

            var gps = _gps.GetCoordinates(file);
            points.Add((file, dateResult.Date.Value, gps?.lat, gps?.lng));
        }

        return points;
    }

    public Task<HomeAwayResult> GenerateHomeAwayFoldersAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var points = GatherDateAndGpsPoints(libraryPath, cancellationToken);
        var geotagged = points.Where(p => p.Lat is not null && p.Lng is not null).ToList();
        var home = FindHome(geotagged, HomeBucketDegrees);

        var homeFolderPath = Path.Combine(libraryPath, RootFolderName, "Home");
        var awayFolderPath = Path.Combine(libraryPath, RootFolderName, "Outside");

        if (home is null)
        {
            LinkFiles([], homeFolderPath);
            LinkFiles([], awayFolderPath);
            return Task.FromResult(new HomeAwayResult
            {
                HomeCount = 0,
                AwayCount = 0,
                UngeotaggedCount = points.Count,
                HomeFolderPath = homeFolderPath,
                AwayFolderPath = awayFolderPath,
            });
        }

        var homeFiles = new List<string>();
        var awayFiles = new List<string>();

        foreach (var p in geotagged)
        {
            var distanceKm = HaversineKm(home.Value.lat, home.Value.lng, p.Lat!.Value, p.Lng!.Value);
            (distanceKm > AwayFromHomeKm ? awayFiles : homeFiles).Add(p.Path);
        }

        var homeLinked = LinkFiles(homeFiles, homeFolderPath);
        var awayLinked = LinkFiles(awayFiles, awayFolderPath);

        _logger.LogInformation(
            "Home/Away split for {LibraryPath}: {Home} home, {Away} away, {Ungeo} without GPS",
            libraryPath, homeLinked.Count, awayLinked.Count, points.Count - geotagged.Count);

        return Task.FromResult(new HomeAwayResult
        {
            HomeCount = homeLinked.Count,
            AwayCount = awayLinked.Count,
            UngeotaggedCount = points.Count - geotagged.Count,
            HomeFolderPath = homeFolderPath,
            AwayFolderPath = awayFolderPath,
        });
    }

    public Task<List<TripFolderResult>> GenerateTripFoldersAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var points = GatherDateAndGpsPoints(libraryPath, cancellationToken);
        var sorted = points.OrderBy(p => p.Date).ToList();

        const double maxGapDaysAway = 3;
        const int minFiles = 15;
        const double minSpanHours = 20;
        const double maxSpanDays = 45; // sanity cap - a single "trip" longer than this is almost certainly a clustering mistake

        var geotagged = points.Where(p => p.Lat is not null && p.Lng is not null).ToList();
        var home = FindHome(geotagged, HomeBucketDegrees);

        // Only points meaningfully far from home can start/extend a trip - photos
        // taken during ordinary life at home never chain into a multi-week blob
        // just because she takes photos every few days.
        var awayPoints = home is null
            ? []
            : geotagged
                .Where(p => HaversineKm(home.Value.lat, home.Value.lng, p.Lat!.Value, p.Lng!.Value) > AwayFromHomeKm)
                .OrderBy(p => p.Date)
                .ToList();

        var awayClusters = new List<List<(string Path, DateTime Date, double? Lat, double? Lng)>>();
        List<(string Path, DateTime Date, double? Lat, double? Lng)>? current = null;

        foreach (var p in awayPoints)
        {
            var startNew = current is null || (p.Date - current![^1].Date).TotalDays > maxGapDaysAway;

            if (startNew)
            {
                current = [];
                awayClusters.Add(current);
            }

            current!.Add(p);
        }

        var results = new List<TripFolderResult>();

        // Two separate trips can easily share the same "Country Year" name
        // (e.g. two different Denmark trips in 2013) - LinkFiles clears its
        // target folder on every call, so without disambiguating, the second
        // trip sharing a name would silently wipe out the first one's folder.
        var usedTripNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var awayCluster in awayClusters)
        {
            var start = awayCluster[0].Date;
            var end = awayCluster[^1].Date;
            if ((end - start).TotalDays > maxSpanDays) continue;

            // Backfill: pull in every file (geotagged or not) that falls inside the
            // trip's date window, so photos taken indoors/offline during the trip
            // aren't left out just because they lack GPS.
            var windowStart = start.AddHours(-12);
            var windowEnd = end.AddHours(12);
            var fullCluster = sorted.Where(p => p.Date >= windowStart && p.Date <= windowEnd).ToList();

            if (fullCluster.Count < minFiles) continue;
            if ((end - start).TotalHours < minSpanHours) continue;

            var country = GuessCountry(awayCluster);
            var baseName = country is not null
                ? $"{country} {start:yyyy}"
                : $"Rejse {start:yyyy-MM-dd} til {end:yyyy-MM-dd}";

            var name = baseName;
            if (!usedTripNames.Add(name))
            {
                name = $"{baseName} ({start:d. MMMM})";
                var attempt = 2;
                while (!usedTripNames.Add(name))
                    name = $"{baseName} ({start:d. MMMM} #{attempt++})";
            }

            var folderPath = Path.Combine(libraryPath, RootFolderName, "Trips", SanitizeName(name));
            LinkFiles(fullCluster.Select(c => c.Path), folderPath);

            results.Add(new TripFolderResult
            {
                Name = name,
                Start = start,
                End = end,
                FileCount = fullCluster.Count,
                FolderPath = folderPath,
            });
        }

        _logger.LogInformation("Generated {Count} trip folders for {LibraryPath}", results.Count, libraryPath);

        return Task.FromResult(results);
    }

    // "Home" = the centroid of the densest small-radius cluster of geotagged
    // photos in the whole library. Bucketing at ~5km cells and picking the
    // most-populated bucket is a cheap stand-in for "where do most of your
    // photos happen to be taken" without needing the user to configure anything.
    private static (double lat, double lng)? FindHome(
        List<(string Path, DateTime Date, double? Lat, double? Lng)> geotagged,
        double bucketDegrees)
    {
        if (geotagged.Count == 0) return null;

        var buckets = geotagged
            .GroupBy(p => (
                Lat: Math.Floor(p.Lat!.Value / bucketDegrees),
                Lng: Math.Floor(p.Lng!.Value / bucketDegrees)))
            .OrderByDescending(g => g.Count())
            .First();

        return (buckets.Average(p => p.Lat!.Value), buckets.Average(p => p.Lng!.Value));
    }

    public Task<YearbookResult?> GenerateYearbookAsync(string libraryPath, int year, CancellationToken cancellationToken = default)
    {
        // Images only - video date extraction shells out to ffprobe per file,
        // which on a large, video-heavy library turns this into a multi-hour
        // scan for what's meant to be a quick "year in review" sample.
        var files = EnumerateLibraryImages(libraryPath).Where(MediaTypeHelper.IsImage).ToList();
        var dated = new List<(string Path, DateTime Date)>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var d = _dateService.GetBestDate(new MediaDateRequest(file));
            if (d.Date is not null && d.Date.Value.Year == year)
                dated.Add((file, d.Date.Value));
        }

        if (dated.Count == 0) return Task.FromResult<YearbookResult?>(null);

        // Spread the selection across the year rather than picking the first N
        // chronologically - up to 8 evenly-spaced photos per month that had any.
        var selected = dated
            .GroupBy(x => x.Date.Month)
            .SelectMany(g =>
            {
                var ordered = g.OrderBy(x => x.Date).ToList();
                var step = Math.Max(1, ordered.Count / 8);
                return ordered.Where((_, i) => i % step == 0).Take(8);
            })
            .OrderBy(x => x.Date)
            .ToList();

        var folderPath = Path.Combine(libraryPath, RootFolderName, "Yearbook", year.ToString());
        var mapping = LinkFiles(selected.Select(x => x.Path), folderPath);

        var htmlPath = Path.Combine(folderPath, "index.html");
        File.WriteAllText(htmlPath, BuildYearbookHtml(year, selected, mapping));

        _logger.LogInformation("Generated yearbook for {Year}: {Count} photos at {Path}", year, selected.Count, folderPath);

        return Task.FromResult<YearbookResult?>(new YearbookResult
        {
            Year = year,
            PhotoCount = selected.Count,
            FolderPath = folderPath,
            HtmlPath = htmlPath,
        });
    }

    public async Task SyncGalleryCollectionsAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var smartFoldersRoot = Path.Combine(libraryPath, RootFolderName);
        var collections = new List<MediaCollection>();

        void AddCollection(string name, string folder)
        {
            if (!Directory.Exists(folder)) return;
            var files = Directory.EnumerateFiles(folder).ToList();
            if (files.Count == 0) return;
            collections.Add(new MediaCollection { Name = name, FilePaths = files });
        }

        // Home/Away is a coarse yes/no split, not something worth a customer's
        // attention as a "look what we found" example - a couple of real Trips
        // (each a specific place + date range) demonstrate the same underlying
        // detection in a way that's actually interesting to browse. Capped so a
        // library with dozens of detected trips doesn't turn "an example" into
        // a wall of cards.
        //
        // Away-from-home clustering fires on every gap, not just real vacations -
        // a library can end up with a couple of real trips abroad alongside dozens
        // of near-noise "Danmark ..." weekend clusters. Prefer trips whose name
        // isn't a bare "Danmark ..."/"Rejse ..." fallback (i.e. a named country
        // GuessCountry actually resolved), then the largest of those, so the
        // example is an actual vacation rather than an arbitrary weekend.
        const int maxTripCollections = 5;
        var tripsRoot = Path.Combine(smartFoldersRoot, "Trips");
        if (Directory.Exists(tripsRoot))
        {
            // Select the biggest/most-real vacations (so the example is an
            // actual trip, not an arbitrary weekend), but then display them
            // smallest-first - a quick "oh, a weekend away" before the bigger
            // "USA 2015" reveal reads better than leading with the biggest.
            var chosenTrips = Directory.EnumerateDirectories(tripsRoot)
                .Select(d => (Dir: d, Name: Path.GetFileName(d), FileCount: Directory.EnumerateFiles(d).Count()))
                .Where(t => t.FileCount > 0)
                .OrderByDescending(t => IsNamedTrip(t.Name))
                .ThenByDescending(t => t.FileCount)
                .Take(maxTripCollections)
                .OrderBy(t => t.FileCount);

            foreach (var trip in chosenTrips)
                AddCollection(trip.Name, trip.Dir);
        }

        var peopleRoot = Path.Combine(smartFoldersRoot, "People");
        if (Directory.Exists(peopleRoot))
        {
            foreach (var personDir in Directory.EnumerateDirectories(peopleRoot))
                AddCollection(Path.GetFileName(personDir), personDir);
        }

        // One collection per year, not one giant merged "Årbøger" bucket -
        // browsing every year's yearbook photos as a single undifferentiated
        // pile isn't useful; each year is its own card.
        var yearbookRoot = Path.Combine(smartFoldersRoot, "Yearbook");
        if (Directory.Exists(yearbookRoot))
        {
            foreach (var yearDir in Directory.EnumerateDirectories(yearbookRoot).OrderBy(Path.GetFileName))
            {
                var year = Path.GetFileName(yearDir);
                var yearFiles = Directory.EnumerateFiles(yearDir)
                    .Where(f => !Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (yearFiles.Count > 0)
                    collections.Add(new MediaCollection { Name = $"Årbog {year}", FilePaths = yearFiles });
            }
        }

        await _collectionStore.SaveAsync(libraryPath, collections);

        _logger.LogInformation(
            "Synced {Count} gallery collections for {LibraryPath}: {Names}",
            collections.Count, libraryPath, string.Join(", ", collections.Select(c => c.Name)));
    }

    private static string BuildYearbookHtml(int year, List<(string Path, DateTime Date)> selected, Dictionary<string, string> mapping)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"da\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Årbog {year}</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("""
            <style>
              body{background:#0b1220;color:#eef2ff;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:0;padding:2rem 1rem}
              h1{text-align:center;font-size:1.8rem}
              .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:1rem;max-width:1200px;margin:2rem auto 0}
              .grid figure{margin:0;background:#111a2e;border:1px solid #223154;border-radius:12px;overflow:hidden}
              .grid img, .grid video{width:100%;display:block;aspect-ratio:1;object-fit:cover}
              .grid figcaption{font-size:.72rem;color:#7b8aad;text-align:center;padding:.4rem}
            </style>
            """);
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>Årbog {year}</h1>");
        sb.AppendLine("<div class=\"grid\">");

        foreach (var item in selected)
        {
            if (!mapping.TryGetValue(item.Path, out var fileName)) continue;

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext is ".heic" or ".heif") continue; // not renderable directly by browsers; still present in the folder itself

            var encodedName = WebUtility.HtmlEncode(fileName);
            var media = MediaTypeHelper.IsVideo(fileName)
                ? $"<video src=\"{encodedName}\" muted preload=\"metadata\" controls></video>"
                : $"<img src=\"{encodedName}\" loading=\"lazy\">";

            sb.AppendLine($"<figure>{media}<figcaption>{item.Date:d. MMMM}</figcaption></figure>");
        }

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string? GuessCountry(List<(string Path, DateTime Date, double? Lat, double? Lng)> cluster)
    {
        var withGps = cluster.Where(c => c.Lat is not null && c.Lng is not null).ToList();
        if (withGps.Count == 0) return null;

        var avgLat = withGps.Average(c => c.Lat!.Value);
        var avgLng = withGps.Average(c => c.Lng!.Value);

        foreach (var box in CountryBoxes)
        {
            if (avgLat >= box.MinLat && avgLat <= box.MaxLat && avgLng >= box.MinLng && avgLng <= box.MaxLng)
                return box.Name;
        }

        return null;
    }

    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    // Every destination file is a symlink back to the original - falls back to a
    // real copy only if symlink creation isn't permitted (e.g. running directly on
    // Windows outside a container, without Developer Mode). Clears any previous
    // run's output first so a regenerate reflects the current match set exactly.
    private static Dictionary<string, string> LinkFiles(IEnumerable<string> sourcePaths, string destFolder)
    {
        Directory.CreateDirectory(destFolder);

        foreach (var existing in Directory.EnumerateFiles(destFolder))
        {
            try { File.Delete(existing); }
            catch { /* best effort */ }
        }

        var mapping = new Dictionary<string, string>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sourcePaths)
        {
            if (!File.Exists(source)) continue;

            var name = Path.GetFileName(source);
            var finalName = name;
            var i = 1;
            while (!usedNames.Add(finalName))
            {
                finalName = $"{Path.GetFileNameWithoutExtension(name)}_{i}{Path.GetExtension(name)}";
                i++;
            }

            var linkPath = Path.Combine(destFolder, finalName);

            try
            {
                File.CreateSymbolicLink(linkPath, source);
            }
            catch
            {
                try { File.Copy(source, linkPath, overwrite: true); }
                catch { continue; }
            }

            mapping[source] = finalName;
        }

        return mapping;
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Unavngivet" : cleaned;
    }

    // A trip named "Danmark ..." or the bare "Rejse <start> til <end>" fallback
    // came from GenerateTripFoldersAsync failing to resolve a real country (see
    // GuessCountry) - almost always just an ordinary away-from-home weekend, not
    // a vacation worth showing off as an example.
    private static bool IsNamedTrip(string name) =>
        !name.StartsWith("Danmark ", StringComparison.OrdinalIgnoreCase) &&
        !name.Equals("Danmark", StringComparison.OrdinalIgnoreCase) &&
        !name.StartsWith("Rejse ", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateLibraryImages(string libraryPath)
    {
        if (!Directory.Exists(libraryPath)) yield break;

        foreach (var file in EnumerateDirectory(libraryPath))
            yield return file;
    }

    private static IEnumerable<string> EnumerateDirectory(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (MediaTypeHelper.IsImage(file) || MediaTypeHelper.IsVideo(file))
                yield return file;
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subDir);
            if (SkippedFolders.Contains(name) || name.StartsWith('@') || name.StartsWith('#') || name.StartsWith('.'))
                continue;

            foreach (var file in EnumerateDirectory(subDir))
                yield return file;
        }
    }
}
