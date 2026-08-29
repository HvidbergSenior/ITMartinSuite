using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.Package3;

public sealed class StaticGalleryExportService : IStaticGalleryExportService
{
    // Sits alongside the real library, never mixed with SmartFolders' symlink
    // output - this folder's thumbnails are real files, safe to copy anywhere
    // (an external drive, a different PC) and still work with no server at all.
    public const string RootFolderName = "_Galleri";
    private const string UnknownYearLabel = "Ukendt dato";
    private const string ThumbExtension = ".jpg";

    private static readonly HashSet<string> SkippedFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "@eadir", "#recycle", "#snapshot", ".@__thumb",
            "@recently-snapshot", ".synophoto", ".package1", ".package2", ".package3",
            "thumbnails", "working", "enhanced", "manifests", "temp",
            "SmartFolders", RootFolderName,
            "LivePhotos",
            // Handled entirely by BuildFileFolderPagesAsync instead - walking them
            // here would sweep incidental images (album art, scanned-doc thumbnails)
            // into the year-based photo grid as if they were real photos. Tenants'
            // libraries use either Danish or English category names depending on
            // when/how they were sorted (e.g. Mie has "Musik", Rico has "Audio") -
            // both must be covered, not just whichever one was checked first.
            "Musik", "Audio", "Dokumenter", "Documents",
            // Flagged-as-duplicate copies staged for review/deletion, not real
            // library content - walking this folder shows every kept photo a
            // second time (found on Rico's library: 17,260 files in here alone).
            "DeleteCandidates",
        };

    private const string ScreenshotFolderName = "Skærmbilleder";

    // Top-level folders that get their own browsable section instead of
    // being mixed into the normal year grid: photos re-grouped by camera
    // (see LibraryPolishService.GroupByCameraMakeAsync), and screenshots -
    // most of which have no reliable date at all, so mixing them into the
    // year grid means most end up dumped in "Ukendt dato" as a chat-capture/
    // receipt pile instead of their own section.
    private static readonly (string FolderName, string Kind)[] FlatSections =
    [
        ("Olympus Camera", "kamera"),
        ("Canon Camera", "kamera"),
        (ScreenshotFolderName, "screenshot"),
    ];

    private static bool IsScreenshot(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals(ScreenshotFolderName, StringComparison.OrdinalIgnoreCase));

    private readonly IThumbnailService _thumbnailService;
    private readonly IMediaDateService _dateService;
    private readonly ILogger<StaticGalleryExportService> _logger;

    public StaticGalleryExportService(
        IThumbnailService thumbnailService,
        IMediaDateService dateService,
        ILogger<StaticGalleryExportService> logger)
    {
        _thumbnailService = thumbnailService;
        _dateService = dateService;
        _logger = logger;
    }

    public async Task<StaticGalleryExportResult> ExportAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var files = EnumerateLibraryMedia(libraryPath).ToList();

        // Missing ffmpeg otherwise fails silently, one identical warning per
        // video file (thousands of them on a real library) with no single
        // line pointing at the actual cause - found 2026-08-28 only via a
        // "Filer fundet" count that didn't match the real file count.
        if (files.Any(MediaTypeHelper.IsVideo) && !_thumbnailService.IsFfmpegAvailable())
        {
            _logger.LogError(
                "ffmpeg not found - every video in {LibraryPath} will be silently skipped from the gallery export. " +
                "Bundle ffmpeg\\ffmpeg.exe next to this executable (CopyToOutputDirectory in the .csproj) and re-run.",
                libraryPath);
        }

        // Sibling binary to ffmpeg.exe, same bundling gap, same silent
        // failure mode - VideoMetadataService.GetCreationTime just returns
        // null when ffprobe.exe is missing, so every video falls back to a
        // year-only date instead of its real one (found 2026-08-28 right
        // after fixing the ffmpeg.exe gap: 100% of videos in every year
        // showed "dato ukendt").
        if (files.Any(MediaTypeHelper.IsVideo) &&
            !File.Exists(Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffprobe.exe")) &&
            !(Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator)
                .Where(dir => !string.IsNullOrWhiteSpace(dir))
                .Any(dir => File.Exists(Path.Combine(dir, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe"))))
        {
            _logger.LogError(
                "ffprobe not found - every video in {LibraryPath} will get a year-only placeholder date instead of its real one. " +
                "Bundle ffmpeg\\ffprobe.exe next to this executable (CopyToOutputDirectory in the .csproj) and re-run.",
                libraryPath);
        }

        var galleryRoot = Path.Combine(libraryPath, RootFolderName);
        var thumbsRoot = Path.Combine(galleryRoot, "thumbs");
        Directory.CreateDirectory(thumbsRoot);

        // Precomputed single-threaded so thumbnail names can drop the original
        // extension (e.g. "IMG_1234.jpg" instead of "IMG_1234.jpg.jpg") without
        // racing on collisions - falls back to keeping the original extension
        // only for the rare case of two files sharing a base name in one folder
        // (e.g. a Live Photo's still + video sharing "IMG_1234").
        var thumbRelativePaths = new Dictionary<string, string>();
        var usedThumbNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(libraryPath, file);
            var dir = Path.GetDirectoryName(relative) ?? "";
            var stripped = Path.Combine(dir, Path.GetFileNameWithoutExtension(relative) + ThumbExtension);

            var candidate = usedThumbNames.Add(stripped) ? stripped : relative + ThumbExtension;
            usedThumbNames.Add(candidate);
            thumbRelativePaths[relative] = candidate;
        }

        var items = new List<GalleryItem>();
        var generated = 0;

        var degreeOfParallelism = Math.Max(1, Environment.ProcessorCount);
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism, CancellationToken = cancellationToken };
        var itemLock = new object();

        await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
        {
            var relative = Path.GetRelativePath(libraryPath, file);
            var thumbPath = Path.Combine(thumbsRoot, thumbRelativePaths[relative]);

            if (!File.Exists(thumbPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(thumbPath)!);
                try
                {
                    await _thumbnailService.GenerateAsync(file, thumbPath, ct);
                    Interlocked.Increment(ref generated);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Thumbnail generation failed for {File}", file);
                    return;
                }
            }

            if (!File.Exists(thumbPath)) return;

            var dateResult = _dateService.GetBestDate(new MediaDateRequest(file));
            var date = dateResult.Date;

            // The "Filesystem" fallback (file mtime/ctime) only kicks in when
            // EXIF/video/document metadata and filename patterns all come up
            // empty - and on this library, repeated copy operations have reset
            // those timestamps to whichever day the copy happened, not the
            // original capture date (see MediaDateService.GetSafeFileDate).
            // Package1 already placed the file in a Year/Month folder using a
            // trustworthy signal when it was first sorted - trust that folder
            // location over a re-derived, corrupted timestamp. If there's no
            // Year/Month folder either (genuinely-undated files, e.g. the
            // Udaterede tree), the raw filesystem date is just the copy date
            // and worse than no date at all - bucket the file under "unknown"
            // instead of silently mis-filing it into whatever year the last
            // copy happened to run.
            if (!dateResult.IsReliable && dateResult.Source == "Filesystem")
            {
                date = TryGetDateFromFolderPath(libraryPath, file);
            }

            lock (itemLock)
            {
                items.Add(new GalleryItem(file, thumbPath, date, MediaTypeHelper.IsVideo(file), dateResult.IsYearOnly));
                if (items.Count % 500 == 0)
                    _logger.LogInformation("Static gallery export progress: {Done}/{Total}", items.Count, files.Count);
            }
        });

        // Flat sections (camera re-groupings, screenshots) get their own
        // browsable page below instead of the normal year grid - a
        // camera-grouped photo still has a real EXIF date and would
        // otherwise also appear in its year page (shown twice); a screenshot
        // usually has no reliable date at all and would otherwise just pile
        // up in "Ukendt dato".
        var flatSectionPaths = FlatSections
            .Select(s => Path.Combine(libraryPath, s.FolderName) + Path.DirectorySeparatorChar)
            .ToList();
        var yearItems = items
            .Where(i => !flatSectionPaths.Any(p => i.SourcePath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var byYear = yearItems
            .GroupBy(i => i.Date?.Year.ToString() ?? UnknownYearLabel)
            // Real years newest-first, but Ukendt dato always last - it's a
            // catch-all bucket, not a "year", and reading top-to-bottom as a
            // timeline is confusing if the undated pile leads it off.
            .OrderBy(g => g.Key == UnknownYearLabel ? 1 : 0)
            .ThenByDescending(g => g.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var yearGroup in byYear)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Year-only items (no real month/day signal, just a folder-name
            // year) all carry the same synthetic Jan-1 Date - sorted last
            // within their year instead of first, so they don't masquerade
            // as "the earliest photos of the year" ahead of genuinely-dated
            // January photos.
            var ordered = yearGroup
                .OrderBy(i => i.IsYearOnly)
                .ThenBy(i => i.Date ?? DateTime.MinValue)
                .ToList();
            var note = yearGroup.Key == UnknownYearLabel
                ? "Disse filer har ingen dato i sig selv, og lå heller ikke i en mappe der afslørede årstallet - " +
                  "derfor kunne de ikke sorteres ind under et bestemt år. Skal et billede flyttes til det rigtige år: " +
                  "find filen under \"Udaterede\" (vist i klammer under hvert billede herunder), flyt den til den " +
                  "rigtige Year/N Måned-mappe i Billeder eller Videoer, og kør eksporten igen. " +
                  "Eller nemmere: du kan manuelt notere datoerne på disse billeder og give dem til mig, så lægger jeg dem rigtigt ind for dig."
                : null;

            // Billeder and Videoer are separate folders per year (not one mixed
            // grid) - matches how Musik/Dokumenter already work, and keeps a
            // sideways video thumbnail from reading as a bug sitting next to photos.
            var photos = ordered.Where(i => !i.IsVideo).ToList();
            var videos = ordered.Where(i => i.IsVideo).ToList();
            var subPages = new List<(string Label, string FileName, int Count)>();

            if (photos.Count > 0)
            {
                var fileName = $"{SanitizeFileName(yearGroup.Key)}-billeder.html";
                await File.WriteAllTextAsync(
                    Path.Combine(galleryRoot, fileName),
                    BuildYearMediaPageHtml("Billeder", yearGroup.Key, photos, galleryRoot, $"{yearGroup.Key}.html", note),
                    cancellationToken);
                subPages.Add(("Billeder", fileName, photos.Count));
            }
            if (videos.Count > 0)
            {
                var fileName = $"{SanitizeFileName(yearGroup.Key)}-videoer.html";
                await File.WriteAllTextAsync(
                    Path.Combine(galleryRoot, fileName),
                    BuildYearMediaPageHtml("Videoer", yearGroup.Key, videos, galleryRoot, $"{yearGroup.Key}.html", null),
                    cancellationToken);
                subPages.Add(("Videoer", fileName, videos.Count));
            }

            var yearHtmlPath = Path.Combine(galleryRoot, $"{yearGroup.Key}.html");
            await File.WriteAllTextAsync(yearHtmlPath, BuildFolderIndexHtml(yearGroup.Key, subPages), cancellationToken);
        }

        var smartFolderLinks = await BuildSmartFolderPagesAsync(libraryPath, galleryRoot, thumbsRoot, thumbRelativePaths, cancellationToken);
        smartFolderLinks.AddRange(await BuildFileFolderPagesAsync(libraryPath, galleryRoot, cancellationToken));
        smartFolderLinks.AddRange(await BuildFlatSectionPagesAsync(libraryPath, galleryRoot, items, cancellationToken));

        var indexPath = Path.Combine(libraryPath, "index.html");
        await File.WriteAllTextAsync(indexPath, BuildIndexHtml(byYear, libraryPath, galleryRoot, smartFolderLinks), cancellationToken);

        _logger.LogInformation(
            "Static gallery export complete for {LibraryPath}: {Total} files, {Generated} new thumbnails, {Years} year pages, {SmartFolders} smart-folder pages",
            libraryPath, items.Count, generated, byYear.Count, smartFolderLinks.Count);

        return new StaticGalleryExportResult
        {
            TotalFiles = items.Count,
            ThumbnailsGenerated = generated,
            YearsGenerated = byYear.Count,
            IndexPath = indexPath,
        };
    }

    private sealed record GalleryItem(string SourcePath, string ThumbPath, DateTime? Date, bool IsVideo, bool IsYearOnly = false);

    private sealed record SmartFolderLink(string Kind, string Label, string Href, string? CoverThumbPath);

    // Builds one browsable, thumbnail-backed page per SmartFolders leaf (each
    // person, Home/Outside, each trip) by resolving every symlink back to its
    // real file and reusing the thumbnail already generated for it in the main
    // pass - no extra thumbnailing work. Yearbook already has its own
    // hand-built HTML from SmartFoldersService, so it's linked as-is rather
    // than regenerated.
    private async Task<List<SmartFolderLink>> BuildSmartFolderPagesAsync(
        string libraryPath, string galleryRoot, string thumbsRoot,
        Dictionary<string, string> thumbRelativePaths, CancellationToken cancellationToken)
    {
        var links = new List<SmartFolderLink>();
        var smartFoldersRoot = Path.Combine(libraryPath, "SmartFolders");
        if (!Directory.Exists(smartFoldersRoot)) return links;

        async Task AddPageAsync(string kind, string label, string slug, IEnumerable<string> folderFiles)
        {
            var pageItems = await BuildSmartFolderItemsAsync(folderFiles, libraryPath, thumbsRoot, thumbRelativePaths, cancellationToken);
            if (pageItems.Count == 0) return;

            var fileName = $"{kind}-{SanitizeFileName(slug)}.html";
            var pagePath = Path.Combine(galleryRoot, fileName);
            await File.WriteAllTextAsync(pagePath, BuildYearHtml(label, pageItems, galleryRoot), cancellationToken);

            var cover = pageItems.FirstOrDefault(i => !i.IsVideo && !IsScreenshot(i.SourcePath))
                ?? pageItems.FirstOrDefault(i => !i.IsVideo)
                ?? pageItems.FirstOrDefault();
            var hrefFromLibraryRoot = ToWebPath(Path.Combine(Path.GetFileName(galleryRoot), fileName));
            links.Add(new SmartFolderLink(kind, label, hrefFromLibraryRoot, cover?.ThumbPath));
        }

        // Home/Away is a coarse yes/no split, not worth showing as an example -
        // a couple of real Trips demonstrate the same detection more clearly.
        // Capped so a library with dozens of detected trips doesn't turn "an
        // example" into a wall of pages. Away-from-home clustering fires on every
        // gap, not just real vacations, so prefer trips whose name isn't a bare
        // "Danmark ..."/"Rejse ..." fallback (see SmartFoldersService.IsNamedTrip),
        // then the largest of those - an actual vacation, not an arbitrary weekend.
        const int maxTripPages = 5;
        var tripsRoot = Path.Combine(smartFoldersRoot, "Trips");
        if (Directory.Exists(tripsRoot))
        {
            var chosenTrips = Directory.EnumerateDirectories(tripsRoot)
                .Select(d => (Dir: d, Name: Path.GetFileName(d), FileCount: Directory.EnumerateFiles(d).Count()))
                .Where(t => t.FileCount > 0)
                .OrderByDescending(t => !t.Name.StartsWith("Danmark", StringComparison.OrdinalIgnoreCase) &&
                                        !t.Name.StartsWith("Rejse ", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(t => t.FileCount)
                .Take(maxTripPages);

            foreach (var trip in chosenTrips)
                await AddPageAsync("tur", trip.Name, trip.Name, Directory.EnumerateFiles(trip.Dir));
        }

        var peopleRoot = Path.Combine(smartFoldersRoot, "People");
        if (Directory.Exists(peopleRoot))
        {
            foreach (var personDir in Directory.EnumerateDirectories(peopleRoot))
            {
                var name = Path.GetFileName(personDir);
                await AddPageAsync("person", name, name, Directory.EnumerateFiles(personDir));
            }
        }

        var yearbookRoot = Path.Combine(smartFoldersRoot, "Yearbook");
        if (Directory.Exists(yearbookRoot))
        {
            foreach (var yearDir in Directory.EnumerateDirectories(yearbookRoot))
            {
                var existingHtml = Path.Combine(yearDir, "index.html");
                if (!File.Exists(existingHtml)) continue;

                var year = Path.GetFileName(yearDir);
                var href = ToWebPath(Path.GetRelativePath(libraryPath, existingHtml));
                // Reuse one of the yearbook's own copied photos as the card cover
                // (full-resolution, not a generated thumbnail - fine for a local
                // file:// viewer where there's no network transfer cost) rather
                // than leaving the card blank like a plain text link.
                var cover = Directory.EnumerateFiles(yearDir).FirstOrDefault(IsWebSafeImage);
                links.Add(new SmartFolderLink("aarbog", $"Årbog {year}", href, cover));
            }
        }

        return links;
    }

    // SmartFolders entries are real copied files in practice (not symlinks -
    // the "resolve back to the original" path below is a no-op fallback for a
    // format SmartFoldersService doesn't actually produce here), so they never
    // hit thumbRelativePaths (keyed by the main library tree, which explicitly
    // skips the SmartFolders subtree). Generate a thumbnail for the copy
    // itself instead of assuming one already exists under the original's path.
    private async Task<List<GalleryItem>> BuildSmartFolderItemsAsync(
        IEnumerable<string> folderFiles, string libraryPath, string thumbsRoot,
        Dictionary<string, string> thumbRelativePaths, CancellationToken cancellationToken)
    {
        var items = new List<GalleryItem>();
        foreach (var file in folderFiles)
        {
            var real = ResolveOriginalPath(file);
            var relative = Path.GetRelativePath(libraryPath, real);

            string thumbPath;
            if (thumbRelativePaths.TryGetValue(relative, out var thumbRel))
            {
                thumbPath = Path.Combine(thumbsRoot, thumbRel);
            }
            else
            {
                var ownRelative = Path.GetRelativePath(libraryPath, file);
                var strippedRel = Path.Combine(
                    Path.GetDirectoryName(ownRelative) ?? "",
                    Path.GetFileNameWithoutExtension(ownRelative) + ThumbExtension);
                thumbPath = Path.Combine(thumbsRoot, strippedRel);

                if (!File.Exists(thumbPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(thumbPath)!);
                    try
                    {
                        await _thumbnailService.GenerateAsync(real, thumbPath, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Thumbnail generation failed for {File}", real);
                        continue;
                    }
                }
            }

            if (!File.Exists(thumbPath)) continue;

            var dateResult = _dateService.GetBestDate(new MediaDateRequest(real));
            items.Add(new GalleryItem(real, thumbPath, dateResult.Date, MediaTypeHelper.IsVideo(real), dateResult.IsYearOnly));
        }

        return items;
    }

    // Musik/Dokumenter never make it into the by-year grid (EnumerateLibraryMedia
    // only walks image/video files), so without this they'd be invisible from the
    // offline viewer - present as one flat, thumbnail-free page per folder instead
    // (playable <audio> for tracks, plain download links for documents).
    // One page per leaf folder ("headline" - an album, or a Year/Month bucket)
    // plus a landing page of folder cards, mirroring how Trips/People work -
    // browsing 3,733 tracks or 873 documents as one giant flat list doesn't scale.
    private async Task<List<SmartFolderLink>> BuildFileFolderPagesAsync(
        string libraryPath, string galleryRoot, CancellationToken cancellationToken)
    {
        var links = new List<SmartFolderLink>();
        // Only one of each pair will actually exist for a given library - Danish
        // vs English category names depending on when/how it was sorted.
        foreach (var (folderName, kind) in new[]
                 {
                     ("Musik", "musik"), ("Audio", "musik"),
                     ("Dokumenter", "dokumenter"), ("Documents", "dokumenter"),
                 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderPath = Path.Combine(libraryPath, folderName);
            if (!Directory.Exists(folderPath)) continue;

            // iTunes' own library keeps a huge nested cache of per-track cover
            // art (Album Artwork\...\<hash>\NN\NN\NN\<file>, one image per
            // leaf folder) and internal database/plist files alongside the
            // real songs - none of that is music to browse, and left in it
            // turns the index into hundreds of junk "1 file" album cards.
            var files = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Album Artwork{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(f => kind != "musik" || MediaTypeHelper.IsAudio(f))
                .ToList();
            if (files.Count == 0) continue;

            var headlineCards = new List<(string Label, string FileName, int Count)>();
            var byHeadline = files
                .GroupBy(f => Path.GetDirectoryName(Path.GetRelativePath(folderPath, f)) ?? "")
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in byHeadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var headlineLabel = string.IsNullOrEmpty(group.Key) ? folderName : group.Key.Replace('\\', '/');
                var orderedFiles = group.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

                var pageFileName = $"{kind}-{SanitizeFileName(group.Key)}.html";
                var pagePath = Path.Combine(galleryRoot, pageFileName);
                await File.WriteAllTextAsync(
                    pagePath, BuildFileListHtml(headlineLabel, orderedFiles, galleryRoot, $"{kind}.html"), cancellationToken);

                headlineCards.Add((headlineLabel, pageFileName, orderedFiles.Count));
            }

            var indexFileName = $"{kind}.html";
            await File.WriteAllTextAsync(
                Path.Combine(galleryRoot, indexFileName), BuildFolderIndexHtml(folderName, headlineCards), cancellationToken);

            var href = ToWebPath(Path.Combine(Path.GetFileName(galleryRoot), indexFileName));
            links.Add(new SmartFolderLink(kind, $"{folderName} ({files.Count})", href, CoverThumbPath: null));
        }

        return links;
    }

    // Reuses thumbnails already generated in the main pass (these files were
    // never excluded from EnumerateLibraryMedia, just pulled out of the
    // year grid above) - no extra thumbnailing work needed here.
    private async Task<List<SmartFolderLink>> BuildFlatSectionPagesAsync(
        string libraryPath, string galleryRoot, List<GalleryItem> items, CancellationToken cancellationToken)
    {
        var links = new List<SmartFolderLink>();

        foreach (var (folderName, kind) in FlatSections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderPath = Path.Combine(libraryPath, folderName) + Path.DirectorySeparatorChar;
            var folderItems = items
                .Where(i => i.SourcePath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Date ?? DateTime.MinValue)
                .ToList();
            if (folderItems.Count == 0) continue;

            var photos = folderItems.Where(i => !i.IsVideo).ToList();
            var videos = folderItems.Where(i => i.IsVideo).ToList();
            var subPages = new List<(string Label, string FileName, int Count)>();
            var slug = SanitizeFileName(folderName);

            if (photos.Count > 0)
            {
                var fileName = $"{slug}-billeder.html";
                await File.WriteAllTextAsync(
                    Path.Combine(galleryRoot, fileName),
                    BuildYearMediaPageHtml("Billeder", folderName, photos, galleryRoot, $"{slug}.html", null),
                    cancellationToken);
                subPages.Add(("Billeder", fileName, photos.Count));
            }
            if (videos.Count > 0)
            {
                var fileName = $"{slug}-videoer.html";
                await File.WriteAllTextAsync(
                    Path.Combine(galleryRoot, fileName),
                    BuildYearMediaPageHtml("Videoer", folderName, videos, galleryRoot, $"{slug}.html", null),
                    cancellationToken);
                subPages.Add(("Videoer", fileName, videos.Count));
            }

            var indexFileName = $"{slug}.html";
            await File.WriteAllTextAsync(
                Path.Combine(galleryRoot, indexFileName), BuildFolderIndexHtml(folderName, subPages), cancellationToken);

            // Prefer the most recent reliably-dated photo for the card cover -
            // sorting undated items to DateTime.MinValue (see folderItems above)
            // meant they always won FirstOrDefault(), so a section's cover was
            // effectively a random undated screenshot/photo rather than anything
            // representative.
            var cover = photos.Where(i => i.Date.HasValue).OrderByDescending(i => i.Date).FirstOrDefault(i => !IsScreenshot(i.SourcePath))?.ThumbPath
                ?? photos.Where(i => i.Date.HasValue).OrderByDescending(i => i.Date).FirstOrDefault()?.ThumbPath
                ?? photos.FirstOrDefault()?.ThumbPath;
            var href = ToWebPath(Path.Combine(Path.GetFileName(galleryRoot), indexFileName));
            links.Add(new SmartFolderLink(kind, folderName, href, cover));
        }

        return links;
    }

    private static string BuildFolderIndexHtml(string sectionLabel, List<(string Label, string FileName, int Count)> cards)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"da\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>{WebUtility.HtmlEncode(sectionLabel)}</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("""
            <style>
              body{background:#0b1220;color:#eef2ff;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:0;padding:1.5rem 1rem 4rem}
              h1{text-align:center;font-size:1.6rem;margin-bottom:.25rem}
              a.back{display:block;text-align:center;color:#7b8aad;text-decoration:none;margin-bottom:1rem}
              .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:1rem;max-width:1200px;margin:1rem auto 0}
              .card{background:#111a2e;border:1px solid #223154;border-radius:14px;padding:1rem 1.2rem;text-decoration:none;color:inherit;display:block}
              .card .label{font-size:1.05rem}
              .card .count{color:#7b8aad;font-size:.85rem;margin-top:.2rem}
            </style>
            """);
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>{WebUtility.HtmlEncode(sectionLabel)}</h1>");
        sb.AppendLine("<a class=\"back\" href=\"../index.html\">&larr; Forside</a>");
        sb.AppendLine("<div class=\"grid\">");
        foreach (var card in cards)
        {
            sb.AppendLine($"<a class=\"card\" href=\"{card.FileName}\"><div class=\"label\">{WebUtility.HtmlEncode(card.Label)}</div>" +
                          $"<div class=\"count\">{card.Count} filer</div></a>");
        }
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string BuildFileListHtml(string label, List<string> files, string galleryRoot, string backHref)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"da\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>{WebUtility.HtmlEncode(label)}</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("""
            <style>
              body{background:#0b1220;color:#eef2ff;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:0;padding:1.5rem 1rem 4rem}
              h1{text-align:center;font-size:1.6rem;margin-bottom:.25rem}
              a.back{display:block;text-align:center;color:#7b8aad;text-decoration:none;margin-bottom:1rem}
              .wrap{max-width:800px;margin:0 auto}
              ul{list-style:none;padding:0;margin:0}
              li{padding:.5rem 0;border-bottom:1px solid #182347}
              a.file{color:#eef2ff;text-decoration:none}
              a.file:hover{color:#8fa1d0}
              audio{width:100%;margin-top:.3rem;height:2rem}
            </style>
            """);
        sb.AppendLine("</head><body><div class=\"wrap\">");
        sb.AppendLine($"<h1>{WebUtility.HtmlEncode(label)}</h1>");
        sb.AppendLine($"<a class=\"back\" href=\"{backHref}\">&larr; Tilbage</a>");
        sb.AppendLine("<ul>");
        foreach (var file in files)
        {
            var href = ToWebPath(Path.GetRelativePath(galleryRoot, file));
            var name = Path.GetFileName(file);
            sb.AppendLine(MediaTypeHelper.IsAudio(file)
                ? $"<li>{WebUtility.HtmlEncode(name)}<audio controls preload=\"none\" src=\"{href}\"></audio></li>"
                : $"<li><a class=\"file\" href=\"{href}\" download>{WebUtility.HtmlEncode(name)}</a></li>");
        }
        sb.AppendLine("</ul></div></body></html>");
        return sb.ToString();
    }

    // SmartFolders entries are symlinks back to the real library file wherever
    // the OS/environment allows it (falls back to a real copy otherwise, per
    // SmartFoldersService) - resolve back to the original so the mirrored
    // thumbnail lookup (keyed by the original's relative path) still hits.
    private static string ResolveOriginalPath(string path)
    {
        try
        {
            var target = File.ResolveLinkTarget(path, returnFinalTarget: true);
            return target?.FullName ?? path;
        }
        catch
        {
            return path;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "unavngivet" : cleaned;
    }

    // A year's Billeder/Videoer are each their own generated page (not a mixed
    // grid) - matches how Musik/Dokumenter already browse, and a sideways video
    // thumbnail sitting next to photos used to read as a bug rather than the
    // separate, unrelated problem it actually is.
    private static string BuildYearMediaPageHtml(
        string sectionLabel, string yearLabel, List<GalleryItem> items, string galleryRoot, string backHref, string? note)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"da\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>{WebUtility.HtmlEncode(sectionLabel)} {WebUtility.HtmlEncode(yearLabel)}</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("""
            <style>
              body{background:#0b1220;color:#eef2ff;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:0;padding:1.5rem 1rem 4rem}
              h1{text-align:center;font-size:1.6rem;margin-bottom:.25rem}
              a.back{display:block;text-align:center;color:#7b8aad;text-decoration:none;margin-bottom:1rem}
              p.note{max-width:700px;margin:0 auto 1.2rem;color:#7b8aad;font-size:.85rem;text-align:center;line-height:1.5}
              .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:.6rem;max-width:1400px;margin:0 auto}
              .grid figure{margin:0;cursor:pointer;background:#111a2e;border:1px solid #223154;border-radius:8px;overflow:hidden}
              .grid img{width:100%;display:block;aspect-ratio:1;object-fit:cover}
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
        sb.AppendLine($"<h1>{WebUtility.HtmlEncode(sectionLabel)} - {WebUtility.HtmlEncode(yearLabel)}</h1>");
        sb.AppendLine($"<a class=\"back\" href=\"{backHref}\">&larr; {WebUtility.HtmlEncode(yearLabel)}</a>");
        if (note is not null)
            sb.AppendLine($"<p class=\"note\">{WebUtility.HtmlEncode(note)}</p>");
        sb.AppendLine("<div class=\"grid\" id=\"grid\"></div>");

        sb.AppendLine("<div class=\"lightbox\" id=\"lb\"><button class=\"close\" onclick=\"closeLb()\">&times;</button>" +
                      "<button class=\"nav prev\" onclick=\"step(-1)\">&#8249;</button>" +
                      "<button class=\"nav next\" onclick=\"step(1)\">&#8250;</button>" +
                      "<div id=\"lbMedia\"></div><div class=\"caption\" id=\"lbCaption\"></div></div>");

        sb.AppendLine("<script>const items = [");
        foreach (var item in items)
        {
            var thumb = ToWebPath(Path.GetRelativePath(galleryRoot, item.ThumbPath));
            var full = ToWebPath(Path.GetRelativePath(galleryRoot, item.SourcePath));
            // A year-only Date is a synthetic Jan-1 placeholder (see
            // MediaDateService's ParentFolderYear fallback) - showing it as
            // "1. January yyyy" claims a specific day that was never actually
            // known, and silently clusters every undated photo in the year
            // under a fake New Year's Day. Say plainly that only the year is
            // known instead.
            var caption = item.IsYearOnly
                ? $"{item.Date:yyyy} (dato ukendt)"
                : item.Date?.ToString("d. MMMM yyyy") ?? full;
            sb.Append("{t:\"").Append(JsEscape(thumb)).Append("\",f:\"").Append(JsEscape(full))
              .Append("\",v:").Append(item.IsVideo ? "true" : "false")
              .Append(",w:").Append(item.IsVideo || IsWebSafeImage(item.SourcePath) ? "true" : "false")
              .Append(",d:\"").Append(JsEscape(caption)).Append("\"},");
        }
        sb.AppendLine("];");

        sb.AppendLine("""
            const grid = document.getElementById('grid');
            items.forEach((it, i) => {
              const fig = document.createElement('figure');
              const img = document.createElement('img');
              img.src = it.t; img.loading = 'lazy';
              fig.appendChild(img);
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
                : `<img src="${it.w ? it.f : it.t}">`;
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

    private static string BuildYearHtml(string yearLabel, List<GalleryItem> items, string galleryRoot, string? note = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"da\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>{WebUtility.HtmlEncode(yearLabel)}</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("""
            <style>
              body{background:#0b1220;color:#eef2ff;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:0;padding:1.5rem 1rem 4rem}
              h1{text-align:center;font-size:1.6rem;margin-bottom:.25rem}
              h2.section{max-width:1400px;margin:1.5rem auto .6rem;font-size:1rem;color:#c2cbe6;font-weight:600}
              a.back{display:block;text-align:center;color:#7b8aad;text-decoration:none;margin-bottom:1rem}
              p.note{max-width:700px;margin:0 auto 1.2rem;color:#7b8aad;font-size:.85rem;text-align:center;line-height:1.5}
              .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:.6rem;max-width:1400px;margin:0 auto}
              .grid figure{margin:0;cursor:pointer;background:#111a2e;border:1px solid #223154;border-radius:8px;overflow:hidden}
              .grid img{width:100%;display:block;aspect-ratio:1;object-fit:cover}
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
        sb.AppendLine($"<h1>{WebUtility.HtmlEncode(yearLabel)}</h1>");
        sb.AppendLine("<a class=\"back\" href=\"../index.html\">&larr; Alle &aring;r</a>");
        if (note is not null)
            sb.AppendLine($"<p class=\"note\">{WebUtility.HtmlEncode(note)}</p>");

        // Photos and videos get their own grid rather than one mixed timeline -
        // a sideways video thumbnail sitting next to photos reads as a bug even
        // when it's really just an unrelated, separate problem (the fixer only
        // ever handles images, never video rotation).
        var hasPhotos = items.Any(i => !i.IsVideo);
        var hasVideos = items.Any(i => i.IsVideo);
        if (hasPhotos)
        {
            sb.AppendLine("<h2 class=\"section\">Billeder</h2>");
            sb.AppendLine("<div class=\"grid\" id=\"gridPhotos\"></div>");
        }
        if (hasVideos)
        {
            sb.AppendLine("<h2 class=\"section\">Videoer</h2>");
            sb.AppendLine("<div class=\"grid\" id=\"gridVideos\"></div>");
        }

        sb.AppendLine("<div class=\"lightbox\" id=\"lb\"><button class=\"close\" onclick=\"closeLb()\">&times;</button>" +
                      "<button class=\"nav prev\" onclick=\"step(-1)\">&#8249;</button>" +
                      "<button class=\"nav next\" onclick=\"step(1)\">&#8250;</button>" +
                      "<div id=\"lbMedia\"></div><div class=\"caption\" id=\"lbCaption\"></div></div>");

        sb.AppendLine("<script>const items = [");
        foreach (var item in items)
        {
            var thumb = ToWebPath(Path.GetRelativePath(galleryRoot, item.ThumbPath));
            var full = ToWebPath(Path.GetRelativePath(galleryRoot, item.SourcePath));
            // No date to show on the Ukendt dato page - show the file's relative
            // path instead, so it can actually be found and moved to fix it.
            // A year-only date (SmartFolder/person pages can carry one) is a
            // synthetic Jan-1 placeholder, not a real day - say so plainly
            // instead of claiming a specific date that was never known.
            var caption = item.IsYearOnly
                ? $"{item.Date:yyyy} (dato ukendt)"
                : item.Date?.ToString("d. MMMM yyyy") ?? full;
            sb.Append("{t:\"").Append(JsEscape(thumb)).Append("\",f:\"").Append(JsEscape(full))
              .Append("\",v:").Append(item.IsVideo ? "true" : "false")
              .Append(",w:").Append(item.IsVideo || IsWebSafeImage(item.SourcePath) ? "true" : "false")
              .Append(",d:\"").Append(JsEscape(caption)).Append("\"},");
        }
        sb.AppendLine("];");

        sb.AppendLine("""
            const gridPhotos = document.getElementById('gridPhotos');
            const gridVideos = document.getElementById('gridVideos');
            items.forEach((it, i) => {
              const fig = document.createElement('figure');
              const img = document.createElement('img');
              img.src = it.t; img.loading = 'lazy';
              fig.appendChild(img);
              fig.onclick = () => openLb(i);
              (it.v ? gridVideos : gridPhotos)?.appendChild(fig);
            });

            let current = -1;
            const lb = document.getElementById('lb');
            const lbMedia = document.getElementById('lbMedia');
            const lbCaption = document.getElementById('lbCaption');

            function render() {
              const it = items[current];
              lbMedia.innerHTML = it.v
                ? `<video src="${it.f}" controls autoplay></video>`
                : `<img src="${it.w ? it.f : it.t}">`;
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

    private static string BuildIndexHtml(
        List<IGrouping<string, GalleryItem>> byYear, string libraryPath, string galleryRoot,
        List<SmartFolderLink> smartFolderLinks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"da\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>Fotobibliotek</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("""
            <style>
              body{background:#0b1220;color:#eef2ff;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:0;padding:2.5rem 1rem}
              h1{text-align:center;font-size:2rem}
              h2{max-width:1200px;margin:2.5rem auto 0;font-size:1.2rem;color:#c2cbe6;font-weight:600}
              p.sub{text-align:center;color:#7b8aad;margin-top:-.5rem}
              .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:1.2rem;max-width:1200px;margin:1rem auto 0}
              .card{background:#111a2e;border:1px solid #223154;border-radius:14px;overflow:hidden;text-decoration:none;color:inherit;display:block}
              .card img{width:100%;aspect-ratio:1;object-fit:cover;display:block}
              .card .label{padding:.8rem 1rem;font-size:1.1rem}
              .card .count{color:#7b8aad;font-size:.85rem}
              .pill{display:inline-block;font-size:.7rem;letter-spacing:.04em;text-transform:uppercase;color:#8fa1d0;background:#182347;padding:.15rem .5rem;border-radius:999px;margin-bottom:.3rem}
            </style>
            """);
        sb.AppendLine("</head><body>");
        sb.AppendLine("<h1>Velkommen til dit fotobibliotek</h1>");
        sb.AppendLine("<p class=\"sub\">Klik for at gennemse billeder og videoer - virker uden internet.</p>");

        sb.AppendLine("<h2>År</h2>");
        sb.AppendLine("<div class=\"grid\">");
        foreach (var yearGroup in byYear)
        {
            var cover = yearGroup.FirstOrDefault(i => !i.IsVideo && !IsScreenshot(i.SourcePath))
                ?? yearGroup.FirstOrDefault(i => !i.IsVideo)
                ?? yearGroup.First();
            var coverWeb = ToWebPath(Path.GetRelativePath(libraryPath, cover.ThumbPath));
            var href = ToWebPath(Path.Combine(Path.GetFileName(galleryRoot), $"{yearGroup.Key}.html"));
            sb.AppendLine($"<a class=\"card\" href=\"{href}\"><img src=\"{coverWeb}\" loading=\"lazy\">" +
                          $"<div class=\"label\">{WebUtility.HtmlEncode(yearGroup.Key)}<div class=\"count\">{yearGroup.Count()} filer</div></div></a>");
        }
        sb.AppendLine("</div>");

        AppendSmartFolderSection(sb, "Årbøger", "aarbog", smartFolderLinks, galleryRoot, libraryPath);
        AppendSmartFolderSection(sb, "Personer", "person", smartFolderLinks, galleryRoot, libraryPath);
        AppendSmartFolderSection(sb, "Steder", "sted", smartFolderLinks, galleryRoot, libraryPath);
        AppendSmartFolderSection(sb, "Ture", "tur", smartFolderLinks, galleryRoot, libraryPath);
        AppendSmartFolderSection(sb, "Kameraer", "kamera", smartFolderLinks, galleryRoot, libraryPath);
        AppendSmartFolderSection(sb, "Skærmbilleder", "screenshot", smartFolderLinks, galleryRoot, libraryPath);
        AppendSmartFolderSection(sb, "Musik", "musik", smartFolderLinks, galleryRoot, libraryPath);
        AppendSmartFolderSection(sb, "Dokumenter", "dokumenter", smartFolderLinks, galleryRoot, libraryPath);

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendSmartFolderSection(
        StringBuilder sb, string heading, string kind, List<SmartFolderLink> links, string galleryRoot, string libraryPath)
    {
        var matching = links.Where(l => l.Kind == kind).ToList();
        if (matching.Count == 0) return;

        sb.AppendLine($"<h2>{WebUtility.HtmlEncode(heading)}</h2>");
        sb.AppendLine("<div class=\"grid\">");
        foreach (var link in matching)
        {
            var href = link.Href;
            var img = link.CoverThumbPath is not null
                ? $"<img src=\"{ToWebPath(Path.GetRelativePath(libraryPath, link.CoverThumbPath))}\" loading=\"lazy\">"
                : "";
            sb.AppendLine($"<a class=\"card\" href=\"{href}\">{img}<div class=\"label\">{WebUtility.HtmlEncode(link.Label)}</div></a>");
        }
        sb.AppendLine("</div>");
    }

    // Formats browsers can reliably render directly in an <img> tag. HEIC/HEIF
    // (and anything else outside this set) render fine as a JPEG thumbnail in
    // the grid, but opening the raw original in the lightbox would show a
    // broken image in most browsers - fall back to the thumbnail instead.
    private static readonly HashSet<string> WebSafeImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

    private static bool IsWebSafeImage(string path) =>
        WebSafeImageExtensions.Contains(Path.GetExtension(path));

    // Matches the "{Year}/{MM}-{MonthName}" folder convention Package1 sorts
    // into (e.g. "Billeder/2013/03-March/IMG_1234.jpg") - the Year/Month pair is
    // nested one level under the top category folder (Billeder/Videoer/...), not
    // at the relative-path root, so this must match mid-path. Anchored to a path
    // separator (or start-of-string) immediately before the year so a coincidental
    // 4-digit number deeper in a filename never matches.
    private static readonly Regex YearMonthFolderPattern =
        new(@"(?:^|[\\/])(?<year>(19|20)\d{2})[\\/](?<month>0[1-9]|1[0-2])-", RegexOptions.Compiled);

    private static DateTime? TryGetDateFromFolderPath(string libraryPath, string file)
    {
        var relative = Path.GetRelativePath(libraryPath, file);
        var match = YearMonthFolderPattern.Match(relative);
        if (!match.Success) return null;

        var year = int.Parse(match.Groups["year"].Value);
        var month = int.Parse(match.Groups["month"].Value);
        return new DateTime(year, month, 1);
    }

    private static string ToWebPath(string relativePath) => relativePath.Replace('\\', '/');

    private static string JsEscape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static IEnumerable<string> EnumerateLibraryMedia(string libraryPath)
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
