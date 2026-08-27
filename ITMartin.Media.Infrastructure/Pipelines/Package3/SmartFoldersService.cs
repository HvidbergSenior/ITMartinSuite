using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;
using ITMartin.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.Package3;

public sealed class SmartFoldersService : ISmartFoldersService
{
    public const string RootFolderName = "SmartFolders";

    // How far from home counts as away, for trip clustering. Deliberately much
    // larger than a literal "not at home" distance - ordinary Danish life
    // (work, family visits, errands) routinely covers 10-50km from one precise
    // home GPS bucket. A tight threshold here let ordinary local excursions
    // get classified as "away" often enough that they chained (via the 3-day
    // gap rule below) into whatever real trip happened nearby in time, merging
    // weeks of home photos into one mislabeled "trip" folder.
    private const double AwayFromHomeKm = 100;
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
            // Chat captures, receipts, scrolled-song screenshots - not the kind of
            // photo that belongs in a trip/person/yearbook page, even though they
            // pass the same image-file check as real photos.
            "Skærmbilleder",
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

    // How close together (in time, same folder) consecutive shots need to be
    // to count as one burst - phones firing rapid/burst mode land well under this.
    private const double BurstGapSeconds = 3;
    private const int MaxBurstSize = 6; // cap per-burst AI comparison cost/tokens

    // Hard ceiling on real API calls per invocation across the AI-driven
    // add-ons in this file (best-of-burst comparisons, yearbook captions) -
    // see CLAUDE.md "AI/Claude API cost discipline". A library with more
    // work than this needs multiple clicks, on purpose - that's the point.
    private const int MaxCallsPerRun = 500;

    private readonly IDbContextFactory<MediaDbContext> _dbFactory;
    private readonly IPackage3Service _package3;
    private readonly IGpsService _gps;
    private readonly IMediaDateService _dateService;
    private readonly ICollectionStore _collectionStore;
    private readonly IImageAnalysisService _imageAnalysis;
    private readonly IPerceptualHashService _perceptualHash;
    private readonly AnthropicClient? _anthropicClient;
    private readonly ILogger<SmartFoldersService> _logger;

    public SmartFoldersService(
        IDbContextFactory<MediaDbContext> dbFactory,
        IPackage3Service package3,
        IGpsService gps,
        IMediaDateService dateService,
        ICollectionStore collectionStore,
        IImageAnalysisService imageAnalysis,
        IPerceptualHashService perceptualHash,
        IConfiguration configuration,
        ILogger<SmartFoldersService> logger)
    {
        _dbFactory = dbFactory;
        _package3 = package3;
        _gps = gps;
        _dateService = dateService;
        _collectionStore = collectionStore;
        _imageAnalysis = imageAnalysis;
        _perceptualHash = perceptualHash;
        _logger = logger;

        var apiKey = configuration["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            _anthropicClient = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<PersonFolderResult?> GeneratePersonFolderAsync(string libraryPath, Guid personId, double threshold = 0.45, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.People.FindAsync([personId], cancellationToken);
        if (person is null) return null;

        var matches = await _package3.FindMatchesAsync(personId, threshold);
        var filePaths = matches.Select(m => m.MediaFilePath).ToList();
        if (filePaths.Count == 0) return null;

        var folderPath = Path.Combine(libraryPath, RootFolderName, "People", SanitizeName(person.Name));
        var linked = CopyFiles(filePaths, folderPath);

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

    public Task<List<TripFolderResult>> GenerateTripFoldersAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var points = GatherDateAndGpsPoints(libraryPath, cancellationToken);
        var sorted = points.OrderBy(p => p.Date).ToList();

        const double maxGapDaysAway = 3;
        const int minFiles = 21; // more than 20 - a real trip leaves more of a trace than that
        const int minAwayFiles = 5; // the away cluster itself, before backfill - see below
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
        // (e.g. two different Denmark trips in 2013) - CopyFiles clears its
        // target folder on every call, so without disambiguating, the second
        // trip sharing a name would silently wipe out the first one's folder.
        var usedTripNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var awayCluster in awayClusters)
        {
            // The away cluster needs real substance on its own, before the date-window
            // backfill below adds anything - otherwise a handful of stray away points
            // (a GPS blip, a brief errand past the threshold) can anchor a "trip" that's
            // almost entirely backfilled home photos padded up to minFiles.
            if (awayCluster.Count < minAwayFiles) continue;

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

            // Denmark is home - a cluster that never actually left the country
            // is an ordinary away-from-home weekend, not a trip worth its own
            // folder. (Foreign clusters GuessCountry can't identify still get
            // a "Rejse <dates>" folder below - unknown isn't the same as home.)
            if (country == "Danmark") continue;

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
            CopyFiles(fullCluster.Select(c => c.Path), folderPath);

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
        var mapping = CopyFiles(selected.Select(x => x.Path), folderPath);

        var htmlPath = Path.Combine(folderPath, "index.html");
        File.WriteAllText(htmlPath, BuildYearbookHtml(year, selected, mapping, captions: null));

        _logger.LogInformation("Generated yearbook for {Year}: {Count} photos at {Path}", year, selected.Count, folderPath);

        return Task.FromResult<YearbookResult?>(new YearbookResult
        {
            Year = year,
            PhotoCount = selected.Count,
            FolderPath = folderPath,
            HtmlPath = htmlPath,
        });
    }

    // "AI-billedtekster" - a separate, paid step from the free Årbog itself.
    // Rebuilds (Path, Date) from whatever's actually sitting in the yearbook
    // folder rather than reusing GenerateYearbookAsync's in-memory selection,
    // since that's long gone by the time this runs as its own admin action.
    public async Task<YearbookResult?> AddYearbookCaptionsAsync(string libraryPath, int year, CancellationToken cancellationToken = default)
    {
        var folderPath = Path.Combine(libraryPath, RootFolderName, "Yearbook", year.ToString());
        if (!Directory.Exists(folderPath)) return null;

        var files = Directory.EnumerateFiles(folderPath)
            .Where(f => MediaTypeHelper.IsImage(f) || MediaTypeHelper.IsVideo(f))
            .ToList();
        if (files.Count == 0) return null;

        var captionsPath = Path.Combine(folderPath, "captions.json");
        var captions = LoadCaptions(captionsPath);

        // Videos can't be sent to image analysis - captioned files only ever
        // cover the photos in the yearbook, videos just keep showing their date.
        // Yearbook sampling already keeps this small (<=8/month = <=96/year),
        // but the explicit cap + incremental save are kept anyway per CLAUDE.md
        // AI cost discipline, in case the sampling size ever changes.
        var allUncaptioned = files
            .Where(f => MediaTypeHelper.IsImage(f) && !captions.ContainsKey(Path.GetFileName(f)))
            .ToList();
        var uncaptioned = allUncaptioned.Take(MaxCallsPerRun).ToList();

        var sinceSave = 0;
        foreach (var file in uncaptioned)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await _imageAnalysis.AnalyzeImageAsync(file);
                if (!string.IsNullOrWhiteSpace(result.Description))
                {
                    captions[Path.GetFileName(file)] = result.Description;
                    sinceSave++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Caption generation failed for {Path}", file);
            }

            // Same reasoning as ImageTaggingService: don't only save at the
            // very end, or a crash partway through loses everything already paid for.
            if (sinceSave >= 20)
            {
                SaveCaptions(captionsPath, captions);
                sinceSave = 0;
            }
        }

        SaveCaptions(captionsPath, captions);

        var dated = new List<(string Path, DateTime Date)>();
        var mapping = new Dictionary<string, string>();
        foreach (var file in files)
        {
            var d = _dateService.GetBestDate(new MediaDateRequest(file));
            dated.Add((file, d.Date ?? File.GetLastWriteTime(file)));
            mapping[file] = Path.GetFileName(file);
        }
        dated = dated.OrderBy(x => x.Date).ToList();

        var htmlPath = Path.Combine(folderPath, "index.html");
        File.WriteAllText(htmlPath, BuildYearbookHtml(year, dated, mapping, captions));

        _logger.LogInformation(
            "Captioned yearbook for {Year}: {New} newly captioned, {Remaining} remaining (capped at {Cap}/run), {Total} photos total at {Path}",
            year, uncaptioned.Count, allUncaptioned.Count - uncaptioned.Count, MaxCallsPerRun, files.Count, folderPath);

        return new YearbookResult
        {
            Year = year,
            PhotoCount = files.Count,
            FolderPath = folderPath,
            HtmlPath = htmlPath,
        };
    }

    private static Dictionary<string, string> LoadCaptions(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, string>();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static void SaveCaptions(string path, Dictionary<string, string> captions) =>
        File.WriteAllText(path, JsonSerializer.Serialize(captions, new JsonSerializerOptions { WriteIndented = true }));

    // Stable identity for a burst across runs, despite bursts never being
    // stored anywhere themselves - two runs that detect the same set of files
    // grouped together produce the same signature regardless of iteration order.
    private static string BurstSignature(List<string> burst)
    {
        var joined = string.Join('|', burst.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined)));
    }

    private static Dictionary<string, string> LoadDecidedBursts(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, string>();
        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            // A winner from a prior run may have since been moved/deleted -
            // don't carry forward a reference to a file that no longer exists.
            return loaded.Where(kv => File.Exists(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static void SaveDecidedBursts(string path, Dictionary<string, string> decided)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(decided, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task<BestShotResult> PickBestShotsAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var files = EnumerateLibraryImages(libraryPath).Where(MediaTypeHelper.IsImage).ToList();
        var dated = new List<(string Path, DateTime Date)>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var d = _dateService.GetBestDate(new MediaDateRequest(file));
            if (d.Date is not null) dated.Add((file, d.Date.Value));
        }

        // Bursts only ever chain within the same folder - shots seconds apart
        // in different folders are a coincidence, not a rapid-fire series.
        var bursts = new List<List<string>>();
        foreach (var group in dated.GroupBy(x => Path.GetDirectoryName(x.Path)))
        {
            var ordered = group.OrderBy(x => x.Date).ToList();
            List<(string Path, DateTime Date)> current = [];

            foreach (var item in ordered)
            {
                if (current.Count > 0 && (item.Date - current[^1].Date).TotalSeconds <= BurstGapSeconds)
                {
                    current.Add(item);
                }
                else
                {
                    if (current.Count >= 2) bursts.Add(current.Select(x => x.Path).ToList());
                    current = [item];
                }
            }
            if (current.Count >= 2) bursts.Add(current.Select(x => x.Path).ToList());
        }

        var folderPath = Path.Combine(libraryPath, RootFolderName, "BedsteBillede");

        // Bursts are re-detected fresh every run (nothing about them is stored
        // on the MediaFile itself), so "already decided" is tracked by a
        // content signature (hash of the burst's sorted file paths) in a
        // sidecar - same idea as captions.json. Re-running only spends new
        // API calls on bursts that weren't already decided, and the winners
        // already on file are carried forward instead of being lost when
        // CopyFiles rebuilds the destination folder.
        var decidedPath = Path.Combine(folderPath, "decided.json");
        var decided = LoadDecidedBursts(decidedPath);

        var undecided = bursts.Where(b => !decided.ContainsKey(BurstSignature(b))).ToList();
        var toProcess = undecided.Take(MaxCallsPerRun).ToList();
        var skippedBursts = undecided.Count - toProcess.Count;

        foreach (var burst in toProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidates = burst.Take(MaxBurstSize).ToList();
            int? bestIndex;
            try
            {
                bestIndex = await PickBestOfBurstAsync(candidates, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Best-shot comparison failed for a burst of {Count} at {Path}", candidates.Count, candidates[0]);
                bestIndex = null;
            }

            // Falls back to the first shot rather than dropping the burst
            // entirely - still a real reduction (N photos -> 1) even without
            // an AI opinion, and keeps every burst represented in the folder.
            decided[BurstSignature(burst)] = candidates[bestIndex ?? 0];
        }

        SaveDecidedBursts(decidedPath, decided);

        // Cumulative across every run, not just this one - a fresh detection
        // pass can occasionally miss a burst from a prior run (e.g. new
        // photos shifted a grouping boundary); decided.json is the source of
        // truth for what's actually in the output folder, not this run's
        // freshly-detected burst list.
        var mapping = CopyFiles(decided.Values, folderPath);

        _logger.LogInformation(
            "Best-of-burst complete for {LibraryPath}: {Bursts} bursts found, {Processed} newly processed, {Skipped} left for a follow-up run (capped at {Cap}/run), {Picked} photos picked total at {Path}",
            libraryPath, bursts.Count, toProcess.Count, skippedBursts, MaxCallsPerRun, mapping.Count, folderPath);

        return new BestShotResult
        {
            BurstsFound  = bursts.Count,
            PhotosPicked = mapping.Count,
            FolderPath   = folderPath,
        };
    }

    private static readonly Tool PickBestShotTool = new()
    {
        Name = "pick_best",
        Description = "Report which photo in the burst is the best one",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["index"] = JsonSerializer.SerializeToElement(
                    new { type = "integer", description = "0-based index of the sharpest/best-framed photo, with eyes open if there are people" }),
            },
            Required = ["index"],
        },
    };

    private async Task<int?> PickBestOfBurstAsync(List<string> paths, CancellationToken cancellationToken)
    {
        if (_anthropicClient is null || paths.Count < 2) return null;

        var content = new List<ContentBlockParam>
        {
            new TextBlockParam
            {
                Text = "These are near-duplicate photos from the same burst/rapid series, in order. " +
                       "Pick the single best one - sharpest focus, best framing, eyes open if there are people. Call pick_best.",
            },
        };

        foreach (var path in paths)
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            content.Add(new ImageBlockParam
            {
                Source = new Base64ImageSource { Data = Convert.ToBase64String(bytes), MediaType = GetMimeType(path) },
            });
        }

        var request = new MessageCreateParams
        {
            // Haiku, not Opus - see feedback_ai_cost_ceiling: this is bounded by
            // how many real bursts exist in the library, not the whole library,
            // but still should never be an expensive-per-call model.
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 128,
            System = "You compare near-duplicate burst photos and pick the single best one. Always call pick_best.",
            Tools = [PickBestShotTool],
            ToolChoice = new ToolChoiceTool { Name = "pick_best" },
            Messages = [new() { Role = Role.User, Content = content }],
        };

        var response = await _anthropicClient.Messages.Create(request);

        foreach (var block in response.Content)
        {
            if (!block.TryPickToolUse(out var toolUse)) continue;

            var json = JsonSerializer.Serialize(toolUse.Input);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("index", out var idxEl) && idxEl.TryGetInt32(out var idx) &&
                idx >= 0 && idx < paths.Count)
                return idx;
        }

        return null;
    }

    // Fixed calendar dates only - Fastelavn/Easter move year to year and would
    // need real holiday-date logic, not a simple month/day match, so they're
    // left out rather than guessed at.
    private static readonly (string Name, int Month, int DayFrom, int DayTo)[] Traditions =
    [
        ("Jul", 12, 24, 26),
        ("Nytår", 12, 31, 31), // Jan 1 handled separately below - month wraps
    ];

    public Task<List<TraditionResult>> GenerateTraditionsAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var files = EnumerateLibraryImages(libraryPath).Where(MediaTypeHelper.IsImage).ToList();
        var dated = new List<(string Path, DateTime Date)>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var d = _dateService.GetBestDate(new MediaDateRequest(file));
            if (d.Date is not null) dated.Add((file, d.Date.Value));
        }

        var results = new List<TraditionResult>();

        foreach (var tradition in Traditions)
        {
            // Nytår spans New Year's Eve into New Year's Day - Jan 1st belongs
            // to the turn-of-year moment that started on Dec 31st, not treated
            // as its own separate tradition year.
            var matches = tradition.Name == "Nytår"
                ? dated.Where(x => (x.Date.Month == 12 && x.Date.Day == 31) || (x.Date.Month == 1 && x.Date.Day == 1)).ToList()
                : dated.Where(x => x.Date.Month == tradition.Month && x.Date.Day >= tradition.DayFrom && x.Date.Day <= tradition.DayTo).ToList();

            // Dec 31st/Jan 1st belong to the same turn-of-year - group by the
            // year the celebration started in (Dec 31 stays as-is, Jan 1 counts
            // as the previous year's Nytår).
            var byYear = matches
                .GroupBy(x => tradition.Name == "Nytår" && x.Date.Month == 1 ? x.Date.Year - 1 : x.Date.Year)
                .Where(g => g.Any())
                .ToList();

            // Nothing to compare year-over-year with only a single year of
            // photos for this tradition - skip entirely rather than generate
            // a lone, incomparable folder.
            if (byYear.Count < 2) continue;

            foreach (var yearGroup in byYear)
            {
                var folderPath = Path.Combine(libraryPath, RootFolderName, "Traditioner", tradition.Name, yearGroup.Key.ToString());
                var mapping = CopyFiles(yearGroup.Select(x => x.Path), folderPath);
                if (mapping.Count == 0) continue;

                results.Add(new TraditionResult
                {
                    Name       = tradition.Name,
                    YearsFound = byYear.Count,
                    PhotoCount = mapping.Count,
                    FolderPath = folderPath,
                });
            }
        }

        _logger.LogInformation(
            "Traditions complete for {LibraryPath}: {Count} tradition-years generated",
            libraryPath, results.Count);

        return Task.FromResult(results);
    }

    private static string GetMimeType(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg",
        };

    // Batched, not one call per photo - see CLAUDE.md AI cost discipline. Each
    // call sends several photos at once and gets back one estimate per photo.
    private const int UndatedBatchSize = 12;

    private static readonly HashSet<string> KnownUndatedFolderNames =
        new(StringComparer.OrdinalIgnoreCase) { "Undated", "Udaterede" };

    private static readonly HashSet<string> KnownImagesFolderNames =
        new(StringComparer.OrdinalIgnoreCase) { "Images", "Billeder" };

    private static readonly Tool EstimateUndatedYearsTool = new()
    {
        Name = "estimate_years",
        Description = "Report a best-guess year for each photo shown, based purely on visual content",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["estimates"] = JsonSerializer.SerializeToElement(new
                {
                    type = "array",
                    description = "Exactly one entry per photo shown, in the same order",
                    items = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["index"] = new { type = "integer", description = "0-based index of the photo, in the order shown" },
                            ["year"] = new { type = new[] { "integer", "null" }, description = "Best-guess year the photo was taken, or null if there's no usable visual clue (clothing, technology, image quality/era, visible dates, etc.)" },
                            ["confidence"] = new { type = "string", @enum = new[] { "low", "medium", "high" } },
                            ["reason"] = new { type = "string", description = "One short phrase naming the actual visual clue - not a generic explanation" },
                        },
                        required = new[] { "index", "year", "confidence", "reason" },
                    },
                }),
            },
            Required = ["estimates"],
        },
    };

    private sealed record UndatedEstimate(int Index, int? Year, string Confidence, string Reason);

    public async Task<UndatedEstimateResult> EstimateUndatedPhotoYearsAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var undatedRoot = KnownUndatedFolderNames
            .Select(name => Path.Combine(libraryPath, name))
            .FirstOrDefault(Directory.Exists);

        var imagesRoot = KnownImagesFolderNames
            .Select(name => Path.Combine(libraryPath, name))
            .FirstOrDefault(Directory.Exists);

        if (undatedRoot is null || imagesRoot is null)
        {
            return new UndatedEstimateResult();
        }

        var decidedPath = Path.Combine(undatedRoot, "ai_date_estimates.json");
        var decided = LoadUndatedDecisions(decidedPath);

        // Flat scan (Undated has never had Year/Month structure of its own -
        // that's the whole point) - images only, vision can't usefully judge a
        // date from a document or an audio file, and video frames would need
        // ffmpeg extraction first, out of scope for this pass.
        var allCandidates = Directory.EnumerateFiles(undatedRoot)
            .Where(MediaTypeHelper.IsImage)
            .Select(f => Path.GetFileName(f))
            .Where(name => !decided.ContainsKey(name))
            .ToList();

        // Hard cap is on API calls, not files - each call covers a whole batch.
        var maxFiles = UndatedBatchSize * MaxCallsPerRun;
        var toProcess = allCandidates.Take(maxFiles).ToList();

        var moved = 0;
        var lowConfidence = 0;
        var noClue = 0;
        var callsMade = 0;

        foreach (var batch in toProcess.Chunk(UndatedBatchSize))
        {
            if (callsMade >= MaxCallsPerRun) break;
            cancellationToken.ThrowIfCancellationRequested();

            List<UndatedEstimate> estimates;
            try
            {
                estimates = await EstimateBatchAsync(undatedRoot, batch, cancellationToken);
                callsMade++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Undated year-estimation failed for a batch of {Count} starting at {First}", batch.Length, batch[0]);
                continue;
            }

            foreach (var fileName in batch)
            {
                var estimate = estimates.FirstOrDefault(e => e.Index == Array.IndexOf(batch, fileName));
                if (estimate is null)
                {
                    continue; // model dropped this index - leave undecided, retry next run
                }

                if (estimate.Year is null)
                {
                    noClue++;
                    decided[fileName] = new UndatedDecision("none", null, estimate.Reason);
                    continue;
                }

                if (estimate.Confidence is not ("medium" or "high"))
                {
                    lowConfidence++;
                    decided[fileName] = new UndatedDecision("low-confidence", estimate.Year, estimate.Reason);
                    continue;
                }

                var sourcePath = Path.Combine(undatedRoot, fileName);
                var targetDir = Path.Combine(imagesRoot, estimate.Year.Value.ToString(), "Ukendt måned");
                Directory.CreateDirectory(targetDir);

                var targetPath = Path.Combine(targetDir, fileName);
                var i = 1;
                while (File.Exists(targetPath))
                    targetPath = Path.Combine(targetDir, $"{Path.GetFileNameWithoutExtension(fileName)}_{i++}{Path.GetExtension(fileName)}");

                try
                {
                    File.Move(sourcePath, targetPath);
                    moved++;
                    decided[fileName] = new UndatedDecision("moved", estimate.Year, estimate.Reason);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not move {File} to {Target}", sourcePath, targetPath);
                }
            }

            // Same reasoning as ImageTaggingService/BestShot - save incrementally,
            // not only at the end, so a crash partway through doesn't lose
            // decisions (and money) already spent.
            SaveUndatedDecisions(decidedPath, decided);
        }

        return new UndatedEstimateResult
        {
            Processed = moved + lowConfidence + noClue,
            Moved = moved,
            LowConfidenceLeftInPlace = lowConfidence,
            NoUsableClueLeftInPlace = noClue,
            RemainingUnprocessed = allCandidates.Count - (moved + lowConfidence + noClue),
        };
    }

    private async Task<List<UndatedEstimate>> EstimateBatchAsync(string undatedRoot, string[] fileNames, CancellationToken cancellationToken)
    {
        if (_anthropicClient is null) return [];

        var content = new List<ContentBlockParam>
        {
            new TextBlockParam
            {
                Text = $"These are {fileNames.Length} photos with no reliable date metadata (often Facebook/Messenger " +
                       "downloads that strip EXIF on upload). Look at each one and guess the year it was likely taken, " +
                       "purely from what's visible - clothing/fashion, technology (phones, cars, TVs), photo quality and " +
                       "color grading typical of a given camera era, visible calendars/screens/dates, hairstyles, etc. " +
                       "If a photo gives you nothing to go on (a meme, a screenshot of text, a close-up with no context), " +
                       "say so honestly with year: null rather than guessing. Call estimate_years with exactly one entry " +
                       "per photo, in order.",
            },
        };

        foreach (var fileName in fileNames)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(undatedRoot, fileName), cancellationToken);
            content.Add(new ImageBlockParam
            {
                Source = new Base64ImageSource { Data = Convert.ToBase64String(bytes), MediaType = GetMimeType(fileName) },
            });
        }

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5, // cheap, bulk per-photo work - see CLAUDE.md
            MaxTokens = 1024,
            System = "You estimate the rough year a photo was taken from its visual content alone. " +
                     "Be honest about uncertainty - a wrong guess mis-files a real memory into the wrong year, " +
                     "which is worse than leaving it unsorted. Always call estimate_years.",
            Tools = [EstimateUndatedYearsTool],
            ToolChoice = new ToolChoiceTool { Name = "estimate_years" },
            Messages = [new() { Role = Role.User, Content = content }],
        };

        var response = await _anthropicClient.Messages.Create(request);

        foreach (var block in response.Content)
        {
            if (!block.TryPickToolUse(out var toolUse)) continue;

            var json = JsonSerializer.Serialize(toolUse.Input);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("estimates", out var arr)) continue;

            var results = new List<UndatedEstimate>();
            foreach (var item in arr.EnumerateArray())
            {
                var index = item.GetProperty("index").GetInt32();
                var year = item.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() : (int?)null;
                var confidence = item.TryGetProperty("confidence", out var c) ? c.GetString() ?? "low" : "low";
                var reason = item.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
                results.Add(new UndatedEstimate(index, year, confidence, reason));
            }
            return results;
        }

        return [];
    }

    private sealed record UndatedDecision(string Outcome, int? Year, string Reason);

    private static Dictionary<string, UndatedDecision> LoadUndatedDecisions(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, UndatedDecision>();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, UndatedDecision>>(json) ?? new Dictionary<string, UndatedDecision>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, UndatedDecision>();
        }
    }

    private static void SaveUndatedDecisions(string path, Dictionary<string, UndatedDecision> decided) =>
        File.WriteAllText(path, JsonSerializer.Serialize(decided, new JsonSerializerOptions { WriteIndented = true }));

    // Same two folder-name generations as KnownUndatedFolderNames, plus the
    // FileDiscoveryWorkflowStep catch-all for unrecognized file types - a photo
    // still sitting in either one after IndexFaces/EstimateUndatedDates/
    // ClassifyUnhandled have all run is genuinely leftover, not just early in
    // the pipeline. Checked both slash directions - same reasoning as
    // IsUnderUndatedFolder in Package3Service: this typically runs on a Windows
    // dev box against a library that then gets synced to the NAS/Linux side.
    private static readonly string[] LeftoverFolderNames = ["Undated", "Udaterede", "Unhandled"];

    private static bool IsUnderLeftoverFolder(string path) =>
        LeftoverFolderNames.Any(name =>
            path.Contains($"{Path.DirectorySeparatorChar}{name}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"/{name}/", StringComparison.OrdinalIgnoreCase));

    public async Task<List<PersonFolderResult>> GenerateUnknownPersonFoldersAsync(string libraryPath, double threshold = 0.5, CancellationToken cancellationToken = default)
    {
        // Library-wide clustering, same call the manual "tag a face" UI uses -
        // filtering to just the leftover files happens after, below. Letting
        // dated photos of the same person contribute to a cluster's centroid
        // only improves matching for the leftover files that get kept; it never
        // changes which files end up in the output.
        var clusters = await _package3.DiscoverUnnamedPeopleAsync(libraryPath, threshold);

        var results = new List<PersonFolderResult>();
        var n = 0;

        foreach (var cluster in clusters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var leftoverFiles = cluster.MediaFilePaths.Where(IsUnderLeftoverFolder).ToList();
            if (leftoverFiles.Count < 3) continue; // same noise floor as the source clustering

            n++;
            var folderPath = Path.Combine(libraryPath, RootFolderName, "UkendtePersoner", $"Person {n}");
            var mapping = CopyFiles(leftoverFiles, folderPath);
            if (mapping.Count == 0) continue;

            results.Add(new PersonFolderResult { Name = $"Ukendt person {n}", FileCount = mapping.Count, FolderPath = folderPath });
        }

        _logger.LogInformation(
            "Generated {Count} unknown-person folders from Undated/Unhandled leftovers for {LibraryPath}",
            results.Count, libraryPath);

        return results;
    }

    public async Task SyncGalleryCollectionsAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var smartFoldersRoot = Path.Combine(libraryPath, RootFolderName);
        var collections = new List<MediaCollection>();

        // FilePaths are stored relative to libraryPath, not absolute - an
        // absolute path baked in here only ever resolves on whichever machine
        // ran this sync. This add-on is typically run locally, then the
        // library gets synced to wherever it's actually served (the NAS) -
        // an absolute local path silently breaks every file lookup once that
        // happens. Gallery.Server resolves these relative to its own root.
        void AddCollection(string name, string folder, string type)
        {
            if (!Directory.Exists(folder)) return;
            // index.html/captions.json/decided.json are this folder's own
            // generated sidecars, never real content to show as a "photo".
            // Normalized to forward slashes - "portable across machines" has to
            // mean portable across OS too, not just across Windows machines.
            // This sync typically runs on a Windows dev box, then the library
            // (and this collections.json) gets synced to the NAS where
            // gallery-web runs in a Linux container - Path.Combine there treats
            // a literal backslash as just another filename character, not a
            // separator, so an unnormalized Windows-style relative path here
            // silently resolves to zero files once served (see
            // feedback_walk_through_ux history - this shipped once already).
            var files = Directory.EnumerateFiles(folder)
                .Where(f => MediaTypeHelper.IsImage(f) || MediaTypeHelper.IsVideo(f))
                .Select(f => Path.GetRelativePath(libraryPath, f).Replace('\\', '/'))
                .ToList();
            if (files.Count == 0) return;
            collections.Add(new MediaCollection { Name = name, Type = type, FilePaths = files });
        }

        // Home/Away is a coarse yes/no split, not something worth a customer's
        // attention as a "look what we found" example - a couple of real Trips
        // (each a specific place + date range) demonstrate the same underlying
        // detection in a way that's actually interesting to browse.
        //
        // Away-from-home clustering fires on every gap, not just real vacations -
        // a library can end up with a couple of real trips abroad alongside dozens
        // of near-noise "Danmark ..." weekend clusters (IsNamedTrip filters those
        // out) - and the same country can be visited across several different
        // years, each becoming its own dated cluster. Only the single best trip
        // per country is shown, named "{Land} {År}" - one country revisited three
        // times shouldn't turn "an example" into three near-identical cards.
        var tripsRoot = Path.Combine(smartFoldersRoot, "Trips");
        if (Directory.Exists(tripsRoot))
        {
            var tripYearPattern = new System.Text.RegularExpressions.Regex(@"^(.+?)\s+(\d{4})");

            // Display smallest-first - a quick "oh, a weekend away" before the
            // bigger "USA 2015" reveal reads better than leading with the biggest.
            var bestPerCountry = Directory.EnumerateDirectories(tripsRoot)
                .Select(d => (Dir: d, Name: Path.GetFileName(d), FileCount: Directory.EnumerateFiles(d).Count()))
                .Where(t => t.FileCount > 0 && IsNamedTrip(t.Name))
                .Select(t =>
                {
                    var m = tripYearPattern.Match(t.Name);
                    // Drop any "(1. October)" date-suffix - the collection is
                    // named for the country + year only, not the specific cluster.
                    var country = m.Success ? m.Groups[1].Value : t.Name;
                    var canonicalName = m.Success ? $"{m.Groups[1].Value} {m.Groups[2].Value}" : t.Name;
                    return (t.Dir, t.FileCount, Country: country, CanonicalName: canonicalName);
                })
                .GroupBy(t => t.Country, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(t => t.FileCount).First())
                .OrderBy(t => t.FileCount)
                .ToList();

            foreach (var trip in bestPerCountry)
                AddCollection(trip.CanonicalName, trip.Dir, "Trip");
        }

        var peopleRoot = Path.Combine(smartFoldersRoot, "People");
        if (Directory.Exists(peopleRoot))
        {
            foreach (var personDir in Directory.EnumerateDirectories(peopleRoot))
                AddCollection(Path.GetFileName(personDir), personDir, "Person");
        }

        // Anonymous face clusters from GenerateUnknownPersonFoldersAsync - same
        // "Person" collection type as real named people (the gallery UI already
        // knows how to show that), the folder name itself ("Ukendt person 1")
        // is what tells them apart from a tagged name.
        var unknownPeopleRoot = Path.Combine(smartFoldersRoot, "UkendtePersoner");
        if (Directory.Exists(unknownPeopleRoot))
        {
            foreach (var personDir in Directory.EnumerateDirectories(unknownPeopleRoot))
                AddCollection(Path.GetFileName(personDir), personDir, "Person");
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
                    .Where(f => !Path.GetFileName(f).Equals("captions.json", StringComparison.OrdinalIgnoreCase))
                    .Select(f => Path.GetRelativePath(libraryPath, f).Replace('\\', '/'))
                    .ToList();
                if (yearFiles.Count > 0)
                    collections.Add(new MediaCollection { Name = $"Årbog {year}", Type = "Yearbook", FilePaths = yearFiles });
            }
        }

        AddCollection("Bedste billede", Path.Combine(smartFoldersRoot, "BedsteBillede"), "BestShot");

        // One collection per tradition per year (e.g. "Jul 2023", "Jul 2024")
        // so they sit side by side for an easy year-over-year comparison, same
        // idea as the per-year Årbog cards above.
        var traditionsRoot = Path.Combine(smartFoldersRoot, "Traditioner");
        if (Directory.Exists(traditionsRoot))
        {
            foreach (var traditionDir in Directory.EnumerateDirectories(traditionsRoot).OrderBy(Path.GetFileName))
            {
                var name = Path.GetFileName(traditionDir);
                foreach (var yearDir in Directory.EnumerateDirectories(traditionDir).OrderBy(Path.GetFileName))
                    AddCollection($"{name} {Path.GetFileName(yearDir)}", yearDir, "Tradition");
            }
        }

        await _collectionStore.SaveAsync(libraryPath, collections);

        _logger.LogInformation(
            "Synced {Count} gallery collections for {LibraryPath}: {Names}",
            collections.Count, libraryPath, string.Join(", ", collections.Select(c => c.Name)));
    }

    private static string BuildYearbookHtml(
        int year,
        List<(string Path, DateTime Date)> selected,
        Dictionary<string, string> mapping,
        Dictionary<string, string>? captions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"da\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Årbog {year}</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("""
            <style>
              body{background:#0b1220;color:#eef2ff;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:0;padding:2rem 1rem}
              h1{text-align:center;font-size:1.8rem}
              a.back{display:block;text-align:center;color:#7b8aad;text-decoration:none;margin-bottom:1rem}
              .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:1rem;max-width:1200px;margin:2rem auto 0}
              .grid figure{margin:0;cursor:pointer;background:#111a2e;border:1px solid #223154;border-radius:12px;overflow:hidden}
              .grid img, .grid video{width:100%;display:block;aspect-ratio:1;object-fit:cover}
              .grid figcaption{font-size:.72rem;color:#7b8aad;text-align:center;padding:.4rem}
              .grid figcaption .caption{display:block;color:#c7d2fe;font-size:.78rem;margin-bottom:.15rem}
              .lightbox{display:none;position:fixed;inset:0;background:rgba(4,7,15,.94);z-index:10;align-items:center;justify-content:center;flex-direction:column}
              .lightbox.open{display:flex}
              .lightbox img, .lightbox video{max-width:92vw;max-height:82vh}
              .lightbox .nav{position:absolute;top:0;bottom:0;width:15%;display:flex;align-items:center;font-size:2.5rem;color:#7b8aad;background:none;border:none;cursor:pointer}
              .lightbox .prev{left:0;justify-content:flex-start;padding-left:1rem}
              .lightbox .next{right:0;justify-content:flex-end;padding-right:1rem}
              .lightbox .close{position:absolute;top:1rem;right:1.2rem;font-size:1.8rem;color:#eef2ff;background:none;border:none;cursor:pointer;z-index:2}
              .lightbox .caption{margin-top:.75rem;color:#7b8aad;font-size:.85rem}
            </style>
            """);
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>Årbog {year}</h1>");
        // This page lives at SmartFolders/Yearbook/{year}/index.html - three
        // levels below the library root, where the real index.html (built by
        // StaticGalleryExportService) lives.
        sb.AppendLine("<a class=\"back\" href=\"../../../index.html\">&larr; Forside</a>");
        sb.AppendLine("<div class=\"grid\" id=\"grid\"></div>");
        sb.AppendLine("<div class=\"lightbox\" id=\"lb\"><button class=\"close\" onclick=\"closeLb()\">&times;</button>" +
                      "<button class=\"nav prev\" onclick=\"step(-1)\">&#8249;</button>" +
                      "<button class=\"nav next\" onclick=\"step(1)\">&#8250;</button>" +
                      "<div id=\"lbMedia\"></div><div class=\"caption\" id=\"lbCaption\"></div></div>");

        sb.AppendLine("<script>const items = [");
        foreach (var item in selected)
        {
            if (!mapping.TryGetValue(item.Path, out var fileName)) continue;

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext is ".heic" or ".heif") continue; // not renderable directly by browsers; still present in the folder itself

            var isVideo = MediaTypeHelper.IsVideo(fileName);
            var caption = captions is not null && captions.TryGetValue(fileName, out var c) && !string.IsNullOrWhiteSpace(c)
                ? $"{c} - {item.Date:d. MMMM}"
                : item.Date.ToString("d. MMMM");

            sb.Append("{f:\"").Append(JsEscape(fileName)).Append("\",v:").Append(isVideo ? "true" : "false")
              .Append(",d:\"").Append(JsEscape(caption)).Append("\"},");
        }
        sb.AppendLine("];");

        sb.AppendLine("""
            const grid = document.getElementById('grid');
            items.forEach((it, i) => {
              const fig = document.createElement('figure');
              const media = it.v
                ? Object.assign(document.createElement('video'), { src: it.f, muted: true, preload: 'metadata', controls: false })
                : Object.assign(document.createElement('img'), { src: it.f, loading: 'lazy' });
              fig.appendChild(media);
              const caption = document.createElement('figcaption');
              caption.textContent = it.d;
              fig.appendChild(caption);
              fig.onclick = () => openLb(i);
              grid.appendChild(fig);
            });

            let current = -1;
            const lb = document.getElementById('lb');
            const lbMedia = document.getElementById('lbMedia');
            const lbCaption = document.getElementById('lbCaption');

            function render() {
              const it = items[current];
              lbMedia.innerHTML = it.v
                ? `<video src="${it.f}" controls autoplay></video>`
                : `<img src="${it.f}">`;
              lbCaption.textContent = it.d;
            }
            function openLb(i) { current = i; render(); lb.classList.add('open'); }
            function closeLb() { lb.classList.remove('open'); lbMedia.innerHTML = ''; }
            function step(delta) {
              current = (current + delta + items.length) % items.length;
              render();
            }
            document.addEventListener('keydown', e => {
              if (!lb.classList.contains('open')) return;
              if (e.key === 'Escape') closeLb();
              if (e.key === 'ArrowLeft') step(-1);
              if (e.key === 'ArrowRight') step(1);
            });
            """);
        sb.AppendLine("</script></body></html>");
        return sb.ToString();
    }

    private static string JsEscape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // Classifies each point individually and takes the majority country, rather
    // than averaging every point's lat/lng first and bounding-boxing the
    // average - a cluster spanning two real locations (e.g. home in Denmark
    // plus a trip to Crete) averages to a coordinate near neither, which can
    // land inside a third country's box (Croatia, roughly the midpoint) that
    // nobody actually visited. Per-point majority vote is immune to that.
    private static string? GuessCountry(List<(string Path, DateTime Date, double? Lat, double? Lng)> cluster)
    {
        var perPointCountries = cluster
            .Where(c => c.Lat is not null && c.Lng is not null)
            .Select(c => GuessCountryForPoint(c.Lat!.Value, c.Lng!.Value))
            .Where(name => name is not null)
            .ToList();

        if (perPointCountries.Count == 0) return null;

        return perPointCountries
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;
    }

    private static string? GuessCountryForPoint(double lat, double lng)
    {
        foreach (var box in CountryBoxes)
        {
            if (lat >= box.MinLat && lat <= box.MaxLat && lng >= box.MinLng && lng <= box.MaxLng)
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

    // Every destination file is a real copy, not a symlink - the delivered library
    // ends up on a USB/harddisk handed to the customer, where a symlink's target
    // path no longer exists (or points at the wrong machine entirely), so a linked
    // add-on folder would just be empty/broken once it's off the NAS. Clears any
    // previous run's output first so a regenerate reflects the current match set
    // exactly.
    // Looser than LibraryPolishService's near-duplicate threshold (6) - that one
    // is tuned to catch the same photo re-saved/recompressed. This is deliberately
    // wider so several distinct shots against the same backdrop (same room, same
    // photo session) still cluster together, not just byte-near-identical copies.
    private const int SimilarSceneHammingThreshold = 14;
    private const int MinSimilarSceneClusterSize = 3;

    public async Task<List<SimilarSceneResult>> GenerateSimilarSceneFoldersAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var results = new List<SimilarSceneResult>();
        var files = EnumerateLibraryImages(libraryPath).Where(MediaTypeHelper.IsImage).ToList();
        var clusterIndex = 0;

        // Bucketed per containing folder (same approach as LibraryPolishService's
        // near-duplicate pass) - clustering is O(n^2) per bucket, and comparing
        // every photo in the whole library against every other would be millions
        // of comparisons on a real library. Photos from the same scene/session are
        // already sitting in the same Year/Month folder anyway.
        foreach (var folderGroup in files.GroupBy(f => Path.GetDirectoryName(f) ?? libraryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hashed = new List<(string Path, ulong Hash)>();
            foreach (var file in folderGroup)
            {
                var hash = await _perceptualHash.ComputeAsync(file, cancellationToken);
                if (hash is { } h) hashed.Add((file, h));
            }

            var used = new bool[hashed.Count];
            for (var i = 0; i < hashed.Count; i++)
            {
                if (used[i]) continue;

                var members = new List<string> { hashed[i].Path };
                for (var j = i + 1; j < hashed.Count; j++)
                {
                    if (used[j]) continue;
                    if (_perceptualHash.HammingDistance(hashed[i].Hash, hashed[j].Hash) <= SimilarSceneHammingThreshold)
                    {
                        members.Add(hashed[j].Path);
                        used[j] = true;
                    }
                }

                if (members.Count < MinSimilarSceneClusterSize) continue;
                used[i] = true;
                clusterIndex++;

                var name = $"Gruppe {clusterIndex}";
                var folderPath = Path.Combine(libraryPath, RootFolderName, "Lignende", SanitizeName(name));
                CopyFiles(members, folderPath);

                results.Add(new SimilarSceneResult { Name = name, FileCount = members.Count, FolderPath = folderPath });
            }
        }

        _logger.LogInformation("Generated {Count} similar-scene folders for {LibraryPath}", results.Count, libraryPath);
        return results;
    }

    private static Dictionary<string, string> CopyFiles(IEnumerable<string> sourcePaths, string destFolder)
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

            var destPath = Path.Combine(destFolder, finalName);

            try { File.Copy(source, destPath, overwrite: true); }
            catch { continue; }

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
