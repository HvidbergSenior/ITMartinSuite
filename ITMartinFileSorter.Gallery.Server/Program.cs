using System.Text.Json;
using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var mime = new FileExtensionContentTypeProvider();
mime.Mappings[".mp4"]  = "video/mp4";
mime.Mappings[".mov"]  = "video/quicktime";
mime.Mappings[".mkv"]  = "video/x-matroska";
mime.Mappings[".jpg"]  = "image/jpeg";
mime.Mappings[".jpeg"] = "image/jpeg";
mime.Mappings[".png"]  = "image/png";
mime.Mappings[".webp"] = "image/webp";
mime.Mappings[".gif"]  = "image/gif";
mime.Mappings[".heic"] = "image/heic";
mime.Mappings[".avif"] = "image/avif";
mime.Mappings[".mp3"]  = "audio/mpeg";
mime.Mappings[".m4a"]  = "audio/mp4";
mime.Mappings[".aac"]  = "audio/aac";
mime.Mappings[".wav"]  = "audio/wav";

// Root-level category folders Package1/2/3 create that hold implementation
// detail, redundant companion content, or content already surfaced through
// the "Samlinger" (Collections) row - not something a non-technical viewer
// should browse directly. Hidden only at the library root, never inside a
// real content folder (so a legitimately-named subfolder deeper in someone's
// own photos is never affected). SmartFolders' content (Home/Outside/People/
// Yearbook) is synced into collections.json instead, so it shows as grouped
// cards up top rather than requiring a click into a raw folder.
// Always hidden, for every tenant - internal/generated or needs a human, not
// something any customer should browse directly.
var RootFoldersAlwaysHidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "_Galleri", "DeleteCandidates", "LivePhotos", "SmartFolders",
    // Videos LibraryPolishService couldn't confirm are playable (see
    // LibraryPolishService.UnplayableFolderName) - a human needs to look at
    // these, not a customer browsing their gallery.
    "Afspilningsfejl",
    // Real content, but a raw "here are your undated files"/"here are your
    // duplicates" folder reads as clutter/confusing rather than something
    // worth showing - not something a non-technical viewer needs to browse
    // directly.
    "Undated", "Duplicates", "Musik",
};

// Screenshots was previously hidden globally for every tenant based on one
// customer's (Mie's) explicit request - that preference shouldn't silently
// apply to everyone else. Per-tenant now (Galleries__N__HideScreenshots),
// visible by default; only Mie opts out.
HashSet<string> HiddenFoldersFor(GalleryDef gallery) =>
    gallery.HideScreenshots
        ? new HashSet<string>(RootFoldersAlwaysHidden, StringComparer.OrdinalIgnoreCase) { "Screenshots" }
        : RootFoldersAlwaysHidden;

// Friendly Danish labels for the root-level folders that do stay visible -
// the folder name on disk never changes (other pipeline code depends on the
// exact name), this only changes what's displayed to the viewer.
var RootFolderDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Images"] = "Billeder",
    ["Videos"] = "Videoer",
    ["Audio"] = "Lyd",
    ["Documents"] = "Dokumenter",
};

// Billeder/Videoer are the primary content and should lead - everything else
// sorts after, alphabetically among itself.
var RootFolderSortPriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    ["Images"] = 0,
    ["Videos"] = 1,
};

// Package1 names month folders "NN-EnglishMonth" (e.g. "05-May") regardless of
// gallery language - translate just the month word, at any depth (not only the
// library root, unlike RootFolderDisplayNames), so headers stay Danish once
// you've navigated into Billeder/2024/etc, not just on the root folder cards.
var DanishMonthNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["January"] = "Januar", ["February"] = "Februar", ["March"] = "Marts",
    ["April"] = "April", ["May"] = "Maj", ["June"] = "Juni",
    ["July"] = "Juli", ["August"] = "August", ["September"] = "September",
    ["October"] = "Oktober", ["November"] = "November", ["December"] = "December",
};
var MonthFolderPattern = new System.Text.RegularExpressions.Regex(@"^(\d{2})-([A-Za-z]+)$");

string DanishFolderName(string rawName)
{
    var monthMatch = MonthFolderPattern.Match(rawName);
    if (monthMatch.Success && DanishMonthNames.TryGetValue(monthMatch.Groups[2].Value, out var danishMonth))
        return $"{monthMatch.Groups[1].Value}-{danishMonth}";
    return rawName;
}
mime.Mappings[".flac"] = "audio/flac";
mime.Mappings[".ogg"]  = "audio/ogg";

var galleries = app.Configuration
    .GetSection("Galleries")
    .GetChildren()
    .Select(s => new GalleryDef(
        Slug:        s["Slug"]     ?? "",
        Name:        s["Name"]     ?? "",
        Path:        s["Path"]     ?? "",
        Password:    s["Password"],
        ShowSummary: s.GetValue<bool>("ShowSummary"),
        HideScreenshots: s.GetValue<bool>("HideScreenshots"),
        OnThisDayEnabled: s.GetValue<bool>("OnThisDayEnabled"),
        SearchEnabled: s.GetValue<bool>("SearchEnabled")))
    .Where(g => !string.IsNullOrWhiteSpace(g.Slug) && !string.IsNullOrWhiteSpace(g.Path))
    .ToList();

// /api/browse is read-heavy per folder navigation (a FolderCover() disk check
// per subfolder, a FindLivePhotoVideo() pair of File.Exists per image) against
// a library that essentially never changes mid-session - caching makes
// forward/back navigation between already-visited folders instant instead of
// re-walking the filesystem every time. Short TTL rather than no expiry so a
// freshly-delivered library still shows up without a container restart.
var browseCache = new System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expires, object Payload)>();

// manifest.json is Package1's full per-file record (~tens of thousands of
// entries for a real library) - too big to re-parse on every "På denne dag"
// homepage load. Long TTL because it only changes when Martin re-runs the
// pipeline for that customer, which doesn't happen mid-session.
var manifestCache = new System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expires, Package1Manifest? Manifest)>();
var manifestLoader = new Package1ManifestLoader();

// Guard static library files with cookie auth
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (path.StartsWith("/libraryfiles/", StringComparison.OrdinalIgnoreCase))
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var slug = parts[1];
            var g = galleries.FirstOrDefault(x => x.Slug == slug);
            if (g is not null && !string.IsNullOrEmpty(g.Password))
            {
                var cookie = ctx.Request.Cookies[$"gallery_{slug}"];
                var token  = ctx.Request.Query["token"].ToString();
                if (cookie != g.Password && token != g.Password)
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }
            }
        }
    }
    await next();
});

// PhysicalFileProvider/StaticFileMiddleware reports a symlink's own (lstat)
// size rather than its target's - for SmartFolders' symlinked files that means
// serving a handful of bytes (the length of the target path string) instead of
// the real photo/video. Resolve and serve those ourselves; everything else
// (the vast majority - real files) still goes through StaticFileMiddleware below.
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (!path.StartsWith("/libraryfiles/", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 2) { await next(); return; }

    var g = galleries.FirstOrDefault(x => x.Slug == parts[1]);
    if (g is null) { await next(); return; }

    var relative = string.Join('/', parts.Skip(2));
    var physicalPath = Path.GetFullPath(Path.Combine(g.Path, relative));

    if (!File.Exists(physicalPath) || new FileInfo(physicalPath).LinkTarget is null)
    {
        await next();
        return;
    }

    var realFile = File.ResolveLinkTarget(physicalPath, returnFinalTarget: true) as FileInfo;
    if (realFile is null || !realFile.Exists)
    {
        ctx.Response.StatusCode = 404;
        return;
    }

    mime.TryGetContentType(realFile.FullName, out var contentType);
    var result = Results.File(realFile.FullName, contentType ?? "application/octet-stream", enableRangeProcessing: true);
    await result.ExecuteAsync(ctx);
});

// Mount static files per gallery slug
foreach (var g in galleries)
{
    if (Directory.Exists(g.Path))
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider        = new PhysicalFileProvider(g.Path),
            RequestPath         = $"/libraryfiles/{g.Slug}",
            ContentTypeProvider = mime,
        });
}

// ── API ───────────────────────────────────────────────────────────────────────

// SECURITY FIX (2026-08-01): this previously returned every tenant's name and
// slug with no authentication at all - any visitor to the bare root URL saw
// the full customer list before even logging in. Customers should only ever
// see their own gallery, reached via a direct link (?g=slug), never a picker
// of everyone. Kept for Martin's own admin convenience only, now gated behind
// GalleryAdmin:Pin.
app.MapGet("/api/galleries", (string? adminPin, IConfiguration config) =>
{
    var configuredPin = config["GalleryAdmin:Pin"];
    if (string.IsNullOrEmpty(configuredPin) || adminPin != configuredPin)
        return Results.Unauthorized();

    return Results.Ok(galleries.Select(g => new { g.Slug, g.Name }));
});

// Safe to leave unauthenticated - the caller already has to know the exact
// slug (from their own direct link), so this can't be used to enumerate
// other customers the way the full list above could.
app.MapGet("/api/gallery-info", (string gallery) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    return g is null ? Results.NotFound() : Results.Ok(new { g.Name });
});

app.MapPost("/api/login", (LoginRequest req, HttpContext ctx) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == req.Gallery);
    if (g is null) return Results.NotFound();
    if (string.IsNullOrEmpty(g.Password) || g.Password == req.Password)
    {
        ctx.Response.Cookies.Append($"gallery_{req.Gallery}", req.Password ?? "", new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            MaxAge   = TimeSpan.FromDays(30),
        });
        return Results.Ok();
    }
    return Results.Unauthorized();
});

app.MapPost("/api/logout", (string gallery, HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete($"gallery_{gallery}");
    return Results.Ok();
});

app.MapGet("/api/browse", (string gallery, string? path, HttpContext ctx) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null) return Results.NotFound();
    if (!string.IsNullOrEmpty(g.Password) &&
        ctx.Request.Cookies[$"gallery_{gallery}"] != g.Password)
        return Results.Unauthorized();

    var cacheKey = $"{gallery}|{path}";
    if (browseCache.TryGetValue(cacheKey, out var cachedBrowse) && cachedBrowse.Expires > DateTime.UtcNow)
        return Results.Ok(cachedBrowse.Payload);

    var r       = g.Path;
    var current = string.IsNullOrWhiteSpace(path) ? r : Path.GetFullPath(Path.Combine(r, path));

    if (!Directory.Exists(current)) return Results.NotFound();
    if (!IsSafe(current, r))        return Results.BadRequest("path outside library");

    var atRoot = IsSameDir(current, r);
    var hiddenFolders = HiddenFoldersFor(g);

    FolderEntry ToEntry(string d, string? nameOverride = null) => new(
        nameOverride ?? (atRoot && RootFolderDisplayNames.TryGetValue(Path.GetFileName(d), out var friendly) ? friendly : DanishFolderName(Path.GetFileName(d))),
        Rel(d, r),
        FolderCover(d, r, g.Slug));

    var priorityFolders = Directory.EnumerateDirectories(current)
        .Where(d => !Path.GetFileName(d).StartsWith('.'))
        .Where(d => atRoot && RootFolderSortPriority.ContainsKey(Path.GetFileName(d)))
        .OrderBy(d => RootFolderSortPriority[Path.GetFileName(d)])
        .Select(d => ToEntry(d))
        .ToList();

    var restFolders = Directory.EnumerateDirectories(current)
        .Where(d => !Path.GetFileName(d).StartsWith('.'))
        // "thumbnails" is GalleryThumbnailService's own generated cache, sitting
        // inside every content folder at every depth - the root-level hidden-
        // folder check above only applies at atRoot, so without this a customer
        // browsing into any year/month folder would see a spurious "thumbnails"
        // folder and could click into it to see a grid of meaningless tiny crops.
        .Where(d => !Path.GetFileName(d).Equals("thumbnails", StringComparison.OrdinalIgnoreCase))
        .Where(d => !atRoot || (!hiddenFolders.Contains(Path.GetFileName(d)) && !RootFolderSortPriority.ContainsKey(Path.GetFileName(d))))
        .OrderBy(Path.GetFileName)
        .Select(d => ToEntry(d))
        .ToList();

    // SmartFolders' content (Home/Outside/People/Yearbook) is deliberately
    // NOT surfaced here as browsable folders - it's already shown once, as
    // the "Samlinger" row, via collections.json (see SyncGalleryCollectionsAsync
    // and /api/collections below). Showing it twice, once unlabeled in this
    // plain folder grid and once as a labeled example row, is confusing for a
    // non-technical viewer - keep exactly one place it appears.
    var folders = priorityFolders.Concat(restFolders).ToList();

    var files = Directory.EnumerateFiles(current)
        .Where(IsMedia)
        .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
        .Select(f =>
        {
            var ext = Ext(f);
            var wp  = Web(f, r, g.Slug);
            return new
            {
                name      = Path.GetFileName(f),
                relPath   = Rel(f, r),
                webPath   = wp,
                // Videos: real thumbnail if it exists yet, otherwise null (not
                // the raw video URL - a browser can't render an mp4 as an
                // <img>, that would just show a broken image icon).
                thumb     = IsImg(ext) ? (Thumb(f, r, g.Slug) ?? wp) : (IsVid(ext) ? Thumb(f, r, g.Slug) : null),
                isVideo   = IsVid(ext),
                isAudio   = IsAud(ext),
                liveVideo = FindLivePhotoVideo(f, r, g.Slug),
            };
        })
        .ToList();

    var parentFull = atRoot ? null : Directory.GetParent(current)?.FullName;
    var parentRel  = parentFull is null ? null : NormalizeRel(Rel(parentFull, r));

    var browsePayload = new { atRoot, parentRelPath = parentRel, folders, files };
    browseCache[cacheKey] = (DateTime.UtcNow.AddMinutes(10), browsePayload);
    return Results.Ok(browsePayload);
});

app.MapGet("/api/summary", (string gallery, HttpContext ctx) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null || !g.ShowSummary) return Results.NotFound();
    if (!string.IsNullOrEmpty(g.Password) &&
        ctx.Request.Cookies[$"gallery_{gallery}"] != g.Password)
        return Results.Unauthorized();

    if (!Directory.Exists(g.Path)) return Results.NotFound();

    var hiddenFolders = HiddenFoldersFor(g);

    // Same root-level exclusions as folderCount below (hiddenFolders)
    // plus "thumbnails" wherever it appears - _Galleri/SmartFolders hold generated
    // thumbnails or symlinks back to files already counted, LivePhotos holds the
    // motion-clip companion to a still already counted in Images, and
    // DeleteCandidates holds flagged duplicates that aren't part of the real
    // collection. Leaving any of these in inflates the "Din samling er klar" stats.
    var mediaFiles = Directory.EnumerateFiles(g.Path, "*.*", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}thumbnails{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(f => hiddenFolders.All(hidden =>
            !f.Contains($"{Path.DirectorySeparatorChar}{hidden}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        .Where(IsMedia);

    int photoCount = 0, videoCount = 0, audioCount = 0, totalCount = 0;
    long totalBytes = 0;

    foreach (var f in mediaFiles)
    {
        var ext = Ext(f);
        if (IsImg(ext)) photoCount++;
        else if (IsVid(ext)) videoCount++;
        else if (IsAud(ext)) audioCount++;
        totalCount++;

        totalBytes += new FileInfo(f).Length;
    }

    var folderCount = Directory.EnumerateDirectories(g.Path)
        .Count(d => !Path.GetFileName(d).StartsWith('.') &&
                    !Path.GetFileName(d).Equals("thumbnails", StringComparison.OrdinalIgnoreCase) &&
                    !hiddenFolders.Contains(Path.GetFileName(d)));

    return Results.Ok(new
    {
        totalCount, photoCount, videoCount, audioCount, folderCount,
        totalGb = Math.Round(totalBytes / 1024.0 / 1024.0 / 1024.0, 1),
    });
});

app.MapGet("/api/playlist", (string gallery, string folder, HttpContext ctx) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null) return Results.NotFound();
    if (!string.IsNullOrEmpty(g.Password) &&
        ctx.Request.Cookies[$"gallery_{gallery}"] != g.Password)
        return Results.Unauthorized();

    var r    = g.Path;
    var full = Path.GetFullPath(Path.Combine(r, folder));

    if (!Directory.Exists(full)) return Results.NotFound();
    if (!IsSafe(full, r))        return Results.BadRequest("path outside library");

    var files = Directory.EnumerateFiles(full)
        .Where(IsMedia)
        .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
        .Select(f =>
        {
            var ext = Ext(f);
            return new
            {
                name      = Path.GetFileName(f),
                relPath   = Rel(f, r),
                webPath   = Web(f, r, g.Slug),
                isVideo   = IsVid(ext),
                isAudio   = IsAud(ext),
                liveVideo = FindLivePhotoVideo(f, r, g.Slug),
            };
        })
        .ToList();

    return Results.Ok(new { files });
});

app.MapGet("/api/collections", (string gallery, HttpContext ctx) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null) return Results.NotFound();
    if (!string.IsNullOrEmpty(g.Password) &&
        ctx.Request.Cookies[$"gallery_{gallery}"] != g.Password)
        return Results.Unauthorized();

    var list = LoadCollections(g.Path);

    // Preserve collections.json's own order (SmartFoldersService already
    // orders each kind the way it should read - trips smallest-first,
    // yearbooks oldest-first) rather than flattening everything into one
    // biggest-first list regardless of kind.
    var summaries = list
        .Where(c => c.FilePaths.Count > 0)
        .Select(c => new
        {
            name      = c.Name,
            fileCount = c.FilePaths.Count,
            cover     = c.FilePaths.Select(f => TryThumbOrWeb(f, g.Path, g.Slug)).FirstOrDefault(w => w is not null),
        })
        .ToList();

    return Results.Ok(new { collections = summaries });
});

app.MapGet("/api/collections/files", (string gallery, string name, HttpContext ctx) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null) return Results.NotFound();
    if (!string.IsNullOrEmpty(g.Password) &&
        ctx.Request.Cookies[$"gallery_{gallery}"] != g.Password)
        return Results.Unauthorized();

    var list = LoadCollections(g.Path);
    var col = list.FirstOrDefault(c => c.Name == name);
    if (col is null) return Results.NotFound();

    // captions.json (written by the AI-billedtekster add-on) sits next to the
    // files themselves - one dictionary lookup per unique folder, not per file.
    var captionsByDir = new Dictionary<string, Dictionary<string, string>>();

    var files = col.FilePaths
        .Where(f => File.Exists(f) && IsSafe(f, g.Path))
        .Select(f =>
        {
            var ext = Ext(f);
            var wp  = Web(f, g.Path, g.Slug);
            var dir = Path.GetDirectoryName(f)!;
            if (!captionsByDir.TryGetValue(dir, out var captions))
            {
                captions = LoadCaptions(Path.Combine(dir, "captions.json"));
                captionsByDir[dir] = captions;
            }
            captions.TryGetValue(Path.GetFileName(f), out var caption);

            return new
            {
                name      = Path.GetFileName(f),
                relPath   = Rel(f, g.Path),
                webPath   = wp,
                thumb     = IsImg(ext) ? (Thumb(f, g.Path, g.Slug) ?? wp) : (IsVid(ext) ? Thumb(f, g.Path, g.Slug) : null),
                isVideo   = IsVid(ext),
                isAudio   = IsAud(ext),
                liveVideo = FindLivePhotoVideo(f, g.Path, g.Slug),
                caption   = caption,
            };
        })
        .ToList();

    return Results.Ok(new { files });
});

// "På denne dag" is inherently a moving target (today's date changes daily)
// so unlike the other Temamapper add-ons it's never copied into its own
// SmartFolders subfolder - it's a live read over manifest.json's per-file
// capture dates, same idea as /api/collections reading collections.json,
// just computed on the fly instead of pre-generated.
app.MapGet("/api/on-this-day", async (string gallery, HttpContext ctx) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null || !g.OnThisDayEnabled) return Results.NotFound();
    if (!string.IsNullOrEmpty(g.Password) &&
        ctx.Request.Cookies[$"gallery_{gallery}"] != g.Password)
        return Results.Unauthorized();

    if (!manifestCache.TryGetValue(g.Slug, out var cached) || cached.Expires <= DateTime.UtcNow)
    {
        Package1Manifest? manifest = null;
        try { manifest = await manifestLoader.LoadAsync(g.Path, ctx.RequestAborted); }
        catch { /* no manifest yet for this gallery - treat as empty */ }
        cached = (DateTime.UtcNow.AddHours(6), manifest);
        manifestCache[g.Slug] = cached;
    }

    if (cached.Manifest is null) return Results.Ok(new { years = Array.Empty<object>() });

    var today = DateTime.Today;
    var years = cached.Manifest.MediaFiles
        .Where(f => f.CreatedAt is { } d && d.Month == today.Month && d.Day == today.Day && d.Year < today.Year)
        .Where(f => f.ExportedPath is not null && File.Exists(f.ExportedPath) && IsSafe(f.ExportedPath, g.Path))
        .Where(f => IsImg(Ext(f.ExportedPath!)) || IsVid(Ext(f.ExportedPath!)))
        .GroupBy(f => f.CreatedAt!.Value.Year)
        .OrderByDescending(gr => gr.Key)
        .Select(gr => new
        {
            year  = gr.Key,
            files = gr.Select(f =>
            {
                var ext = Ext(f.ExportedPath!);
                var wp  = Web(f.ExportedPath!, g.Path, g.Slug);
                return new
                {
                    name    = Path.GetFileName(f.ExportedPath!),
                    relPath = Rel(f.ExportedPath!, g.Path),
                    webPath = wp,
                    thumb   = IsImg(ext) ? (Thumb(f.ExportedPath!, g.Path, g.Slug) ?? wp) : Thumb(f.ExportedPath!, g.Path, g.Slug),
                    isVideo = IsVid(ext),
                };
            })
            .ToList(),
        })
        .Where(y => y.files.Count > 0)
        .ToList();

    return Results.Ok(new { years });
});

// Search over AI tags written by ImageTaggingService (Søgning & mærker add-on).
// Same live-read-over-manifest.json approach as On This Day - tags live on
// MediaFile.AiTags, nothing new to store or keep in sync on the Gallery side.
app.MapGet("/api/search", async (string gallery, string q, HttpContext ctx) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null || !g.SearchEnabled) return Results.NotFound();
    if (!string.IsNullOrEmpty(g.Password) &&
        ctx.Request.Cookies[$"gallery_{gallery}"] != g.Password)
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(new { files = Array.Empty<object>() });

    if (!manifestCache.TryGetValue(g.Slug, out var cached) || cached.Expires <= DateTime.UtcNow)
    {
        Package1Manifest? manifest = null;
        try { manifest = await manifestLoader.LoadAsync(g.Path, ctx.RequestAborted); }
        catch { /* no manifest yet for this gallery - treat as empty */ }
        cached = (DateTime.UtcNow.AddHours(6), manifest);
        manifestCache[g.Slug] = cached;
    }

    if (cached.Manifest is null) return Results.Ok(new { files = Array.Empty<object>() });

    var files = cached.Manifest.MediaFiles
        .Where(f => f.AiTags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)))
        .Where(f => f.ExportedPath is not null && File.Exists(f.ExportedPath) && IsSafe(f.ExportedPath, g.Path))
        .Where(f => IsImg(Ext(f.ExportedPath!)) || IsVid(Ext(f.ExportedPath!)))
        .OrderByDescending(f => f.CreatedAt)
        .Select(f =>
        {
            var ext = Ext(f.ExportedPath!);
            var wp  = Web(f.ExportedPath!, g.Path, g.Slug);
            return new
            {
                name    = Path.GetFileName(f.ExportedPath!),
                relPath = Rel(f.ExportedPath!, g.Path),
                webPath = wp,
                thumb   = IsImg(ext) ? (Thumb(f.ExportedPath!, g.Path, g.Slug) ?? wp) : Thumb(f.ExportedPath!, g.Path, g.Slug),
                isVideo = IsVid(ext),
            };
        })
        .ToList();

    return Results.Ok(new { files });
});

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────

static string  Ext(string f)           => Path.GetExtension(f).ToLowerInvariant();
static bool    IsImg(string ext)       => ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".heic" or ".avif";
static bool    IsVid(string ext)       => ext is ".mp4" or ".mov" or ".mkv" or ".avi" or ".m4v" or ".webm" or ".wmv";
static bool    IsAud(string ext)       => ext is ".mp3" or ".m4a" or ".aac" or ".wav" or ".flac" or ".ogg";
static bool    IsMedia(string f)       { var e = Ext(f); return IsImg(e) || IsVid(e) || IsAud(e); }
static bool    IsSafe(string p, string r)    => Path.GetFullPath(p).StartsWith(Path.GetFullPath(r), StringComparison.OrdinalIgnoreCase);
static bool    IsSameDir(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
static string  Rel(string abs, string r)     => Path.GetRelativePath(r, abs).Replace("\\", "/");
static string  NormalizeRel(string rel)      => rel == "." ? "" : rel;
static string  Web(string abs, string r, string slug) => $"/libraryfiles/{slug}/" + Rel(abs, r);

// Samlinger entries (Person/Trip/Yearbook) are SmartFolders symlinks pointing
// back to the real file elsewhere in the library - the generated thumbnails
// live next to the real file's own folder, not next to the symlink, so this
// has to resolve first or every collection view silently falls back to
// full-resolution originals despite thumbnails existing.
static string? Thumb(string f, string r, string slug)
{
    var real = ResolveIfSymlink(f);
    var t = Path.Combine(Path.GetDirectoryName(real)!, "thumbnails", Path.GetFileNameWithoutExtension(real) + ".jpg");
    return File.Exists(t) ? Web(t, r, slug) : null;
}

static string ResolveIfSymlink(string path)
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

// The still and its Live Photo motion clip are exported into separate
// top-level folders (Images/ vs LivePhotos/) by Package1, connected only by
// matching Year/Month/filename - there's no explicit link stored anywhere.
// This reconstructs that link at read time, same idea as Package1's own
// LivePhotoDetectionWorkflowStep pairing logic, just applied on already-organized output.
static string? FindLivePhotoVideo(string imagePath, string r, string slug)
{
    if (!IsImg(Ext(imagePath))) return null;

    var rel = Rel(imagePath, r);
    if (!rel.StartsWith("Images/", StringComparison.OrdinalIgnoreCase)) return null;

    var withoutExt   = Path.ChangeExtension(rel, null) ?? rel;
    var livePhotoRel = "LivePhotos" + withoutExt["Images".Length..];

    foreach (var vidExt in new[] { ".mp4", ".mov" })
    {
        var candidate = Path.Combine(r, livePhotoRel + vidExt);
        if (File.Exists(candidate))
            return Web(candidate, r, slug);
    }

    return null;
}

// Year-level folders (e.g. "Images/2025") never have any files directly
// inside them - the actual photos/thumbnails live one level deeper, under
// each month subfolder. Only checking the immediate folder meant every
// year/top-level card showed a generic placeholder instead of a real cover -
// searches down a few levels (month folders, not the whole library) for the
// first usable cover instead of just the immediate directory.
static string? FolderCover(string dir, string r, string slug, int depth = 3)
{
    var td = Path.Combine(dir, "thumbnails");
    if (Directory.Exists(td))
    {
        var t = Directory.EnumerateFiles(td, "*.jpg").FirstOrDefault();
        if (t is not null) return Web(t, r, slug);
    }

    // Images render fine as a raw <img src> fallback with no thumbnail yet;
    // a video file wouldn't (browsers can't show an mp4 as an image) - videos
    // only ever get a cover via their thumbnails/ entry, checked above.
    var direct = Directory.EnumerateFiles(dir).FirstOrDefault(f => IsImg(Ext(f)));
    if (direct is not null) return Web(direct, r, slug);

    if (depth <= 0) return null;

    foreach (var subDir in Directory.EnumerateDirectories(dir).OrderBy(Path.GetFileName))
    {
        var name = Path.GetFileName(subDir);
        if (name.StartsWith('.') || name.Equals("thumbnails", StringComparison.OrdinalIgnoreCase)) continue;

        var cover = FolderCover(subDir, r, slug, depth - 1);
        if (cover is not null) return cover;
    }

    return null;
}

static List<MediaCollection> LoadCollections(string libraryPath)
{
    var path = Path.Combine(libraryPath, "collections.json");
    if (!File.Exists(path)) return [];
    try
    {
        var json = File.ReadAllText(path);
        var collections = JsonSerializer.Deserialize<List<MediaCollection>>(json) ?? [];

        // FilePaths are stored relative to the library root (portable across
        // whichever machine actually serves the library) - resolve to
        // absolute here, once, so every caller just gets a usable path.
        // Path.IsPathRooted also stays true for pre-existing absolute-style
        // entries (older collections.json files written before this fix, or
        // manually rebased ones) - those pass through unchanged.
        foreach (var c in collections)
        {
            c.FilePaths = c.FilePaths
                .Select(f => Path.IsPathRooted(f) ? f : Path.GetFullPath(Path.Combine(libraryPath, f)))
                .ToList();
        }

        return collections;
    }
    catch (JsonException)
    {
        return [];
    }
}

// AI-billedtekster writes this sidecar next to a generated Yearbook folder's
// files - fileName -> caption. Missing/malformed just means no captions yet.
static Dictionary<string, string> LoadCaptions(string path)
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

static string? TryThumbOrWeb(string f, string r, string slug) =>
    File.Exists(f) && IsSafe(f, r) ? (Thumb(f, r, slug) ?? Web(f, r, slug)) : null;

// ShowSummary: the "Din samling er klar!" hero card (readiness message, date
// range, folder/photo counts) is a one-time customer-handoff moment, not
// something a family member visiting a shared link should see - opt-in per
// gallery (Galleries__N__ShowSummary=true) rather than on by default.
record GalleryDef(string Slug, string Name, string Path, string? Password, bool ShowSummary, bool HideScreenshots, bool OnThisDayEnabled, bool SearchEnabled);
record LoginRequest(string Gallery, string Password);
record FolderEntry(string name, string relPath, string? cover);
