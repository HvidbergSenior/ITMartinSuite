using System.Net;
using System.Text;
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
        };

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

            lock (itemLock)
            {
                items.Add(new GalleryItem(file, thumbPath, date, MediaTypeHelper.IsVideo(file)));
                if (items.Count % 500 == 0)
                    _logger.LogInformation("Static gallery export progress: {Done}/{Total}", items.Count, files.Count);
            }
        });

        var byYear = items
            .GroupBy(i => i.Date?.Year.ToString() ?? UnknownYearLabel)
            .OrderByDescending(g => g.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var yearGroup in byYear)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ordered = yearGroup.OrderBy(i => i.Date ?? DateTime.MinValue).ToList();
            var yearHtmlPath = Path.Combine(galleryRoot, $"{yearGroup.Key}.html");
            await File.WriteAllTextAsync(yearHtmlPath, BuildYearHtml(yearGroup.Key, ordered, galleryRoot), cancellationToken);
        }

        var smartFolderLinks = await BuildSmartFolderPagesAsync(libraryPath, galleryRoot, thumbsRoot, thumbRelativePaths, cancellationToken);

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

    private sealed record GalleryItem(string SourcePath, string ThumbPath, DateTime? Date, bool IsVideo);

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
            var pageItems = BuildSmartFolderItems(folderFiles, libraryPath, thumbsRoot, thumbRelativePaths);
            if (pageItems.Count == 0) return;

            var fileName = $"{kind}-{SanitizeFileName(slug)}.html";
            var pagePath = Path.Combine(galleryRoot, fileName);
            await File.WriteAllTextAsync(pagePath, BuildYearHtml(label, pageItems, galleryRoot), cancellationToken);

            var cover = pageItems.FirstOrDefault(i => !i.IsVideo) ?? pageItems.FirstOrDefault();
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
                links.Add(new SmartFolderLink("aarbog", $"Årbog {year}", href, CoverThumbPath: null));
            }
        }

        return links;
    }

    private static List<GalleryItem> BuildSmartFolderItems(
        IEnumerable<string> folderFiles, string libraryPath, string thumbsRoot,
        Dictionary<string, string> thumbRelativePaths)
    {
        var items = new List<GalleryItem>();
        foreach (var file in folderFiles)
        {
            var real = ResolveOriginalPath(file);
            var relative = Path.GetRelativePath(libraryPath, real);
            if (!thumbRelativePaths.TryGetValue(relative, out var thumbRel)) continue;

            var thumbPath = Path.Combine(thumbsRoot, thumbRel);
            if (!File.Exists(thumbPath)) continue;

            items.Add(new GalleryItem(real, thumbPath, null, MediaTypeHelper.IsVideo(real)));
        }

        return items;
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

    private static string BuildYearHtml(string yearLabel, List<GalleryItem> items, string galleryRoot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"da\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>{WebUtility.HtmlEncode(yearLabel)}</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("""
            <style>
              body{background:#0b1220;color:#eef2ff;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:0;padding:1.5rem 1rem 4rem}
              h1{text-align:center;font-size:1.6rem;margin-bottom:.25rem}
              a.back{display:block;text-align:center;color:#7b8aad;text-decoration:none;margin-bottom:1rem}
              .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:.6rem;max-width:1400px;margin:0 auto}
              .grid figure{margin:0;cursor:pointer;background:#111a2e;border:1px solid #223154;border-radius:8px;overflow:hidden}
              .grid img{width:100%;display:block;aspect-ratio:1;object-fit:cover}
              .lightbox{display:none;position:fixed;inset:0;background:rgba(4,7,15,.94);z-index:10;align-items:center;justify-content:center;flex-direction:column}
              .lightbox.open{display:flex}
              .lightbox img, .lightbox video{max-width:92vw;max-height:82vh}
              .lightbox .nav{position:absolute;top:0;bottom:0;width:15%;display:flex;align-items:center;font-size:2.5rem;color:#7b8aad;background:none;border:none;cursor:pointer}
              .lightbox .prev{left:0;justify-content:flex-start;padding-left:1rem}
              .lightbox .next{right:0;justify-content:flex-end;padding-right:1rem}
              .lightbox .close{position:absolute;top:1rem;right:1.2rem;font-size:1.8rem;color:#eef2ff;background:none;border:none;cursor:pointer}
              .lightbox .caption{margin-top:.75rem;color:#7b8aad;font-size:.85rem}
            </style>
            """);
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>{WebUtility.HtmlEncode(yearLabel)}</h1>");
        sb.AppendLine("<a class=\"back\" href=\"../index.html\">&larr; Alle &aring;r</a>");
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
            var caption = item.Date?.ToString("d. MMMM yyyy") ?? "";
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
            var cover = yearGroup.FirstOrDefault(i => !i.IsVideo) ?? yearGroup.First();
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
