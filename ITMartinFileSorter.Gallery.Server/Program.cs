using System.Text.Json;
using ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;
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

// Root-level category folders QuickSort/2/3 create that hold implementation
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
    // directly. Musik (Audio) is real, wanted content though - shown as its
    // own category, just secondary to Billeder/Videoer.
    "Undated", "Duplicates",
};

// Screenshots was previously hidden globally for every tenant based on one
// customer's (Mie's) explicit request - that preference shouldn't silently
// apply to everyone else. Per-tenant now (Galleries__N__HideScreenshots),
// visible by default; only Mie opts out.
HashSet<string> HiddenFoldersFor(GalleryDef gallery) =>
    gallery.HideScreenshots
        ? new HashSet<string>(RootFoldersAlwaysHidden, StringComparer.OrdinalIgnoreCase) { "Screenshots" }
        : RootFoldersAlwaysHidden;

// Opt-in per tenant (Galleries__N__CoreCategoriesOnly) - for a showcase/demo
// gallery where only the four core content categories should ever be
// visible, regardless of whatever else lands at the library root (Undated,
// Screenshots, DeleteCandidates, future categories, etc). Every existing
// real tenant defaults to false and is unaffected - this is an allowlist,
// not a replacement for the blocklist above.
var CoreCategoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Images", "Videos", "Documents", "Musik",
};

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
// sorts after, alphabetically among itself. QuickSort now sorts new libraries
// straight into Danish-named category folders (see feedback_danish_folder_
// defaults) rather than the English names this dictionary originally
// assumed, so both variants are listed - an on-disk folder is one or the
// other depending on when the tenant's library was first sorted, never both.
var RootFolderSortPriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    ["Images"] = 0, ["Billeder"] = 0,
    ["Videos"] = 1, ["Videoer"] = 1,
    ["Screenshots"] = 2, ["Skærmbilleder"] = 2,
};

// Root folders render as separate visual rows, not one flowing grid. Photo-
// like content (Billeder/Videoer/Skærmbilleder) leads in the top row;
// non-photo content (Dokumenter, LivePhotoVideoer, Musik) trails in the row
// below it instead of mixing in - same Danish/English duplication reasoning
// as RootFolderSortPriority above.
var RootFolderRow = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    ["Images"] = 0, ["Billeder"] = 0,
    ["Videos"] = 0, ["Videoer"] = 0,
    ["Screenshots"] = 0, ["Skærmbilleder"] = 0,
    ["Documents"] = 1, ["Dokumenter"] = 1,
    ["Musik"] = 1,
    ["LivePhotoVideoer"] = 1,
};

// Fallback icon shown when a root category card has no meaningful
// content-based cover - either because none exists yet (Videos before any
// thumbnail is generated) or because a real cover would never be
// meaningful (Documents has no photo-like preview; Musik's own folder is
// full of Windows Media Player's cached album art for whichever track
// happens to be enumerated first, not a representative "cover" - see
// FolderCover's Musik/Documents skip below). Images isn't listed - it
// always has real photo covers, a generic icon would be a downgrade.
var RootFolderIcon = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Videos"] = "🎬",
    ["Documents"] = "📄",
    ["Musik"] = "🎵",
};

// Older QuickSort output names month folders "NN-EnglishMonth" (e.g. "05-May")
// regardless of gallery language - translate just the month word, at any
// depth (not only the library root, unlike RootFolderDisplayNames), so
// headers stay Danish once you've navigated into Billeder/2024/etc, not just
// on the root folder cards. Newer QuickSort output (see feedback_danish_
// folder_defaults) names them "N MonthName" already in Danish, no padding,
// space instead of a hyphen - MonthFolderPattern/MonthLabelFor below handle
// both shapes; this table only ever translates the older English form.
var DanishMonthNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["January"] = "Januar", ["February"] = "Februar", ["March"] = "Marts",
    ["April"] = "April", ["May"] = "Maj", ["June"] = "Juni",
    ["July"] = "Juli", ["August"] = "August", ["September"] = "September",
    ["October"] = "Oktober", ["November"] = "November", ["December"] = "December",
};
var DanishMonthNameValues = new HashSet<string>(DanishMonthNames.Values, StringComparer.OrdinalIgnoreCase);
// Optional " - 1. halvdel"/" - 2. halvdel" suffix (see LibraryExportService's
// MonthHalfSplitThreshold) is captured separately so callers can either keep
// it (DanishFolderName, for display) or strip it (MonthLabelFor, for grouping
// both halves of a month back under one inline label).
// Older QuickSort output: "NN-EnglishMonth" (zero-padded, hyphen, English -
// needs DanishMonthNames translation below). Newer QuickSort output: "N
// MonthName" (unpadded, space, already Danish - see feedback_danish_folder_
// defaults). Both shapes must match or a month folder silently falls out of
// chronological grouping entirely and back to a plain alphabetical folder
// list, where "10 November"/"11 December" sort right after "1 Januar"
// instead of at the end (found 2026-08-28 on a real tenant's 2013 folder).
var MonthFolderPattern = new System.Text.RegularExpressions.Regex(@"^(\d{1,2})[-\s]([A-Za-zÆØÅæøå]+(?:-[A-Za-zÆØÅæøå]+)?)( - [12]\. halvdel)?$");

// LibraryExportService's current date-range grouping (recursive best-gap
// bisection - see project_package1_month_split memory): either "dd-dd
// MonthNameDanish" for a same-month range, or "Abbr-Abbr" for a cross-month
// range. Same shape as LibraryVerifyService's GroupLabelPattern. Already Danish
// and already a display-ready label as-is, unlike MonthFolderPattern above
// (which names things in English and needs DanishMonthNames translation).
var DateRangeFolderPattern = new System.Text.RegularExpressions.Regex(@"^(\d{2}-\d{2} \p{L}+|[A-ZÆØÅ][a-zæøå]{2}-[A-ZÆØÅ][a-zæøå]{2})$");

string DanishFolderName(string rawName)
{
    var monthMatch = MonthFolderPattern.Match(rawName);
    if (!monthMatch.Success) return rawName;
    var monthWord = monthMatch.Groups[2].Value;
    // Already Danish (newer QuickSort output) - the raw name is already
    // display-ready as-is, reformatting it would just introduce a hyphen
    // where the real folder has a space.
    if (DanishMonthNameValues.Contains(monthWord)) return rawName;
    // Older English form - needs translating, and this shape always used a
    // zero-padded "NN-" prefix so reproducing that here is still correct.
    if (DanishMonthNames.TryGetValue(monthWord, out var danishMonth))
        return $"{monthMatch.Groups[1].Value}-{danishMonth}{monthMatch.Groups[3].Value}";
    return rawName;
}

// Just the month part, halvdel suffix stripped - both halves of a split
// month collapse back into one inline label in the flattened view. Also
// recognizes the newer date-range folder names, returned as-is since
// they're already Danish and already a single (non-halvdel-split) group.
string? MonthLabelFor(string rawName)
{
    var m = MonthFolderPattern.Match(rawName);
    if (m.Success)
    {
        var monthWord = m.Groups[2].Value;
        var danish = DanishMonthNameValues.Contains(monthWord)
            ? monthWord
            : DanishMonthNames.TryGetValue(monthWord, out var dm) ? dm : monthWord;
        return $"{m.Groups[1].Value}-{danish}";
    }
    return DateRangeFolderPattern.IsMatch(rawName) ? rawName : null;
}

// Danish month abbreviation (3 letters, as used in the "Abbr-Abbr" date-range
// folder form) -> calendar month number, for sorting that form chronologically.
var DanishMonthAbbrToNumber = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    ["Jan"] = 1, ["Feb"] = 2, ["Mar"] = 3, ["Apr"] = 4, ["Maj"] = 5, ["Jun"] = 6,
    ["Jul"] = 7, ["Aug"] = 8, ["Sep"] = 9, ["Okt"] = 10, ["Nov"] = 11, ["Dec"] = 12,
};

// Leading calendar-month number for a month/date-range folder, purely for
// chronological ordering - never shown to the user, so an imperfect guess
// for an unrecognized shape (falls back to 0) just means "sorts first" rather
// than crashing or breaking the grouping this exists to fix.
int MonthSortKeyFor(string rawName)
{
    var m = MonthFolderPattern.Match(rawName);
    if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) return n;

    var digitMatch = System.Text.RegularExpressions.Regex.Match(rawName, @"\d{1,2}");
    if (digitMatch.Success && int.TryParse(digitMatch.Value, out var d)) return d;

    var abbrMatch = System.Text.RegularExpressions.Regex.Match(rawName, @"[A-ZÆØÅ][a-zæøå]{2}");
    if (abbrMatch.Success && DanishMonthAbbrToNumber.TryGetValue(abbrMatch.Value, out var mn)) return mn;

    return 0;
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
        SearchEnabled: s.GetValue<bool>("SearchEnabled"),
        HideAddons: s.GetValue<bool>("HideAddons"),
        CoreCategoriesOnly: s.GetValue<bool>("CoreCategoriesOnly")))
    .Where(g => !string.IsNullOrWhiteSpace(g.Slug) && !string.IsNullOrWhiteSpace(g.Path))
    .ToList();

// /api/browse is read-heavy per folder navigation (a FolderCover() disk check
// per subfolder, a FindLivePhotoVideo() pair of File.Exists per image) against
// a library that essentially never changes mid-session - caching makes
// forward/back navigation between already-visited folders instant instead of
// re-walking the filesystem every time. Short TTL rather than no expiry so a
// freshly-delivered library still shows up without a container restart.
var browseCache = new System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expires, object Payload)>();

// manifest.json is QuickSort's full per-file record (~tens of thousands of
// entries for a real library) - too big to re-parse on every "På denne dag"
// homepage load. Long TTL because it only changes when Martin re-runs the
// pipeline for that customer, which doesn't happen mid-session.
var manifestCache = new System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expires, QuickSortManifest? Manifest)>();
var manifestLoader = new QuickSortManifestLoader();

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

    // Documents/Musik never get a content-based cover, even at the root: a
    // document has no photo-like preview, and Musik's own folder is full of
    // Windows Media Player's cached album art for whichever track happens to
    // enumerate first - not a meaningful "cover" of the customer's own
    // content. Both always fall back to their RootFolderIcon instead.
    FolderEntry ToEntry(string d, string? nameOverride = null)
    {
        var rawName = Path.GetFileName(d);
        var noCoverCategory = atRoot && (rawName.Equals("Documents", StringComparison.OrdinalIgnoreCase) ||
                                          rawName.Equals("Musik", StringComparison.OrdinalIgnoreCase));
        return new(
            nameOverride ?? (atRoot && RootFolderDisplayNames.TryGetValue(rawName, out var friendly) ? friendly : DanishFolderName(rawName)),
            Rel(d, r),
            noCoverCategory ? null : FolderCover(d, r, g.Slug),
            atRoot && RootFolderRow.TryGetValue(rawName, out var row) ? row : 99,
            atRoot && RootFolderIcon.TryGetValue(rawName, out var icon) ? icon : null);
    }

    // @eaDir/#recycle/#snapshot are Synology-generated system folders that can
    // appear at any depth (@eaDir in particular gets created wherever File
    // Station has ever touched a folder) - not something a customer should
    // ever see as a browsable folder.
    static bool IsSystemFolder(string d)
    {
        var name = Path.GetFileName(d);
        return name.StartsWith('.') || name.StartsWith('@') || name.StartsWith('#');
    }

    // A category folder (Lyd/Memes/Screenshots/...) with zero files anywhere
    // underneath it - e.g. an add-on that never actually produced anything for
    // this tenant - still showed up as a clickable card leading to an empty
    // "Ingen medietiler her" page. Short-circuits on the first file found, so
    // a populated folder costs one EnumerateFiles call, not a full walk.
    static bool HasAnyMediaFile(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir))
            if (IsMedia(f)) return true;

        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            var name = Path.GetFileName(sub);
            if (name.StartsWith('.') || name.StartsWith('@') || name.StartsWith('#')) continue;
            if (name.Equals("thumbnails", StringComparison.OrdinalIgnoreCase)) continue;
            if (HasAnyMediaFile(sub)) return true;
        }
        return false;
    }

    var priorityFolders = Directory.EnumerateDirectories(current)
        .Where(d => !IsSystemFolder(d))
        .Where(d => atRoot && RootFolderSortPriority.ContainsKey(Path.GetFileName(d)))
        .Where(HasAnyMediaFile)
        .OrderBy(d => RootFolderSortPriority[Path.GetFileName(d)])
        .Select(d => ToEntry(d))
        .ToList();

    var restFolders = Directory.EnumerateDirectories(current)
        .Where(d => !IsSystemFolder(d))
        // "thumbnails" is GalleryThumbnailService's own generated cache, sitting
        // inside every content folder at every depth - the root-level hidden-
        // folder check above only applies at atRoot, so without this a customer
        // browsing into any year/month folder would see a spurious "thumbnails"
        // folder and could click into it to see a grid of meaningless tiny crops.
        .Where(d => !Path.GetFileName(d).Equals("thumbnails", StringComparison.OrdinalIgnoreCase))
        .Where(d => !atRoot || (
            g.CoreCategoriesOnly
                ? CoreCategoryNames.Contains(Path.GetFileName(d)) && !RootFolderSortPriority.ContainsKey(Path.GetFileName(d))
                : !hiddenFolders.Contains(Path.GetFileName(d)) && !RootFolderSortPriority.ContainsKey(Path.GetFileName(d))))
        .Where(HasAnyMediaFile)
        // Plain alphabetical sorting a "1 Februar"/"10 November" shaped name
        // puts "10 November"/"11 December" right after "1 Februar" instead of
        // at the end - matches MonthSortKeyFor everywhere else. This only
        // fires when isFlattenableYear (below) didn't already turn these into
        // one continuous view - a year that also has a non-month sibling like
        // "Ukendt måned" fails that all-or-nothing check and falls back to
        // this folder-card list, so it still needs to sort correctly.
        // Calendar order (Januar..December), not newest-first - months read
        // as a sequence within a year, unlike years/dates which read newest-
        // first. Non-month folders (Ukendt måned) have no real position and
        // sort after all real months.
        .OrderByDescending(d => MonthLabelFor(Path.GetFileName(d)) is not null)
        .ThenBy(d => MonthSortKeyFor(Path.GetFileName(d)))
        .ThenBy(Path.GetFileName)
        .Select(d => ToEntry(d))
        .ToList();

    // SmartFolders' content (Home/Outside/People/Yearbook) is deliberately
    // NOT surfaced here as browsable folders - it's already shown once, as
    // the "Samlinger" row, via collections.json (see SyncGalleryCollectionsAsync
    // and /api/collections below). Showing it twice, once unlabeled in this
    // plain folder grid and once as a labeled example row, is confusing for a
    // non-technical viewer - keep exactly one place it appears.
    var folders = priorityFolders.Concat(restFolders).ToList();

    // Musik folders are full of Windows Media Player's cached album art
    // (AlbumArt_{GUID}_Large.jpg, Folder.jpg) sitting next to the actual
    // tracks - browsing in there should show the music, not a wall of cover
    // art thumbnails that aren't even a real photo of anything.
    var inMusikFolder = Rel(current, r).Split('/')[0].Equals("Musik", StringComparison.OrdinalIgnoreCase);

    // A Year folder whose only children are Month folders (LibraryExportService
    // only creates these once a year is "busy" - see MonthSplitThreshold) reads
    // as an extra click for no reason - flatten it into one continuous view with
    // inline month labels instead of forcing a folder-per-month click. Never
    // applies inside Musik (organized by Artist/Album, not Year/Month at all).
    // "- 1. halvdel"/"- 2. halvdel" (see MonthHalfSplitThreshold) collapse back
    // into one label - that split exists for the static HD export's page size,
    // not something the live viewer needs to expose as a separate group.
    var isFlattenableYear =
        !inMusikFolder && !atRoot && folders.Count > 0 &&
        folders.All(f => MonthLabelFor(Path.GetFileName(f.relPath)) is not null);

    IEnumerable<string> filePaths;
    Dictionary<string, string>? monthLabelByPath = null;
    // Chronological sort key per month folder (the leading month number, e.g.
    // "02-Februar" -> 2) - see the ordering fix below for why this exists.
    Dictionary<string, int>? monthSortKeyByPath = null;

    if (isFlattenableYear)
    {
        monthLabelByPath = new Dictionary<string, string>();
        monthSortKeyByPath = new Dictionary<string, int>();
        var monthDirs = Directory.EnumerateDirectories(current).Where(d => !IsSystemFolder(d) && !Path.GetFileName(d).Equals("thumbnails", StringComparison.OrdinalIgnoreCase));
        var gathered = new List<string>();
        foreach (var monthDir in monthDirs)
        {
            var folderName = Path.GetFileName(monthDir);
            var label = MonthLabelFor(folderName);
            if (label is null) continue;
            var sortKey = MonthSortKeyFor(folderName);
            foreach (var f in Directory.EnumerateFiles(monthDir).Where(IsMedia))
            {
                gathered.Add(f);
                monthLabelByPath[f] = label;
                monthSortKeyByPath[f] = sortKey;
            }
        }
        filePaths = gathered;
        folders = []; // flattened - no separate Month folder cards
    }
    else if (folders.Count > 0)
    {
        // A handful of stray files sitting directly in a folder that also has
        // real subfolders to navigate (e.g. loose photos straight in
        // "Billeder" alongside its year folders) renders as a confusing
        // second, unlabelled "Billeder & videoer" grid competing with the
        // real navigation - and QuickSort already has a real home for
        // deliberately-orphaned files ("Ikke i årsmapper"/Udaterede), so this
        // is never the only place they'd be findable. Suppress it here;
        // still shown normally one level deeper where there's nothing else
        // to navigate to and these ARE the real content.
        filePaths = [];
    }
    else
    {
        filePaths = Directory.EnumerateFiles(current).Where(IsMedia);
    }

    // Filesystem LastWriteTimeUtc alone is NOT a safe sort key across a
    // flattened multi-month view - repeated copy/re-import passes on a real
    // tenant library leave this timestamp reflecting when a file was last
    // copied in, not when it was taken, so files from different months end
    // up interleaved. That interleaving is what made the month-divider
    // header (see wwwroot/index.html) flip back and forth and appear to
    // repeat instead of showing once per month. Grouping by the month's own
    // chronological key first keeps every month's files contiguous; within
    // a month, LastWriteTimeUtc is still a reasonable enough tiebreaker.
    var files = filePaths
        .Where(f => !inMusikFolder || IsAud(Ext(f)))
        // Calendar order within the year (Januar..December), matching the
        // folder-card fallback's ordering below - months read as a sequence,
        // not newest-first.
        .OrderBy(f => monthSortKeyByPath?.GetValueOrDefault(f) ?? 0)
        .ThenByDescending(f => File.GetLastWriteTimeUtc(f))
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
                thumb     = IsImg(ext) ? (Thumb(f, r, g.Slug) ?? wp) : (IsVid(ext) ? Thumb(f, r, g.Slug) : (IsAud(ext) ? AudioCover(f, r, g.Slug) : null)),
                isVideo   = IsVid(ext),
                isAudio   = IsAud(ext),
                isDoc     = IsDoc(ext),
                liveVideo = FindLivePhotoVideo(f, r, g.Slug),
                monthLabel = monthLabelByPath?.GetValueOrDefault(f),
            };
        })
        .ToList();

    var parentFull = atRoot ? null : Directory.GetParent(current)?.FullName;
    var parentRel  = parentFull is null ? null : NormalizeRel(Rel(parentFull, r));

    var browsePayload = new { atRoot, parentRelPath = parentRel, folders, files, hideAddons = g.HideAddons };
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
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}@eaDir{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(f => hiddenFolders.All(hidden =>
            !f.Contains($"{Path.DirectorySeparatorChar}{hidden}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        .Where(f => !g.CoreCategoriesOnly || CoreCategoryNames.Contains(Rel(f, g.Path).Split('/')[0]))
        .Where(IsMedia)
        // Same reasoning as the folder browse view above: a Musik folder's
        // cached album art and official music-video clips aren't real family
        // photos/videos and shouldn't inflate those counts.
        .Where(f => !Rel(f, g.Path).Split('/')[0].Equals("Musik", StringComparison.OrdinalIgnoreCase) || IsAud(Ext(f)));

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

// Music tracks (Musik folders, mostly old MP3-rip collections) rarely have a
// folder.jpg sitting next to them the way photo albums do - the actual cover
// art is usually only embedded in the file's own ID3/MP4 tag, so this reads
// it out the same way ITMartinPlayer does.
app.MapGet("/api/embedded-cover", (string gallery, string path, HttpContext ctx) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null) return Results.NotFound();
    if (!string.IsNullOrEmpty(g.Password) &&
        ctx.Request.Cookies[$"gallery_{gallery}"] != g.Password)
        return Results.Unauthorized();

    var full = Path.GetFullPath(Path.Combine(g.Path, path));
    if (!IsSafe(full, g.Path)) return Results.BadRequest("path outside library");
    if (!File.Exists(full))    return Results.NotFound();

    try
    {
        using var tagFile = TagLib.File.Create(full);
        var picture = tagFile.Tag?.Pictures?.FirstOrDefault();
        if (picture is null) return Results.NotFound();
        return Results.Bytes(picture.Data.Data, picture.MimeType ?? "image/jpeg");
    }
    catch
    {
        return Results.NotFound();
    }
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
                isDoc     = IsDoc(ext),
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
            type      = c.Type,
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
                thumb     = IsImg(ext) ? (Thumb(f, g.Path, g.Slug) ?? wp) : (IsVid(ext) ? Thumb(f, g.Path, g.Slug) : (IsAud(ext) ? AudioCover(f, g.Path, g.Slug) : null)),
                isVideo   = IsVid(ext),
                isAudio   = IsAud(ext),
                isDoc     = IsDoc(ext),
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
        QuickSortManifest? manifest = null;
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
        QuickSortManifest? manifest = null;
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
static bool    IsDoc(string ext)       => ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx";
static bool    IsMedia(string f)       { var e = Ext(f); return IsImg(e) || IsVid(e) || IsAud(e) || IsDoc(e); }
static bool    IsSafe(string p, string r)    => Path.GetFullPath(p).StartsWith(Path.GetFullPath(r), StringComparison.OrdinalIgnoreCase);
static bool    IsSameDir(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
static string  Rel(string abs, string r)     => Path.GetRelativePath(r, abs).Replace("\\", "/");
static string  NormalizeRel(string rel)      => rel == "." ? "" : rel;
static string  Web(string abs, string r, string slug) => $"/libraryfiles/{slug}/" + Rel(abs, r);
static string  AudioCover(string f, string r, string slug) => $"/api/embedded-cover?gallery={slug}&path={Uri.EscapeDataString(Rel(f, r))}";

// Samlinger entries (Person/Trip/Yearbook) are SmartFolders' own real copies
// (never symlinks - see ISmartFoldersService docs), so their thumbnails live
// in a "thumbnails" folder right next to them, same as everywhere else.
static string? Thumb(string f, string r, string slug)
{
    var t = Path.Combine(Path.GetDirectoryName(f)!, "thumbnails", Path.GetFileNameWithoutExtension(f) + ".jpg");
    return File.Exists(t) ? Web(t, r, slug) : null;
}

// The still and its Live Photo motion clip are exported into separate
// top-level folders (Images/ vs LivePhotos/) by QuickSort, connected only by
// matching Year/Month/filename - there's no explicit link stored anywhere.
// This reconstructs that link at read time, same idea as QuickSort's own
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
        // Directory.EnumerateFiles has no guaranteed order, and a stray
        // zero-byte file (e.g. a leftover/failed thumbnail generation) sitting
        // in this folder could win the pick by pure filesystem-enumeration
        // luck, rendering as a broken image client-side. Sort for determinism
        // and skip anything that isn't a real file.
        var t = Directory.EnumerateFiles(td, "*.jpg")
            .Where(f => new FileInfo(f).Length > 0)
            .OrderBy(Path.GetFileName)
            .FirstOrDefault();
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
                // Defensive: some collections.json files were written by a
                // Windows dev box with backslash separators, which this Linux
                // container's Path.Combine treats as a literal filename
                // character rather than a directory separator - normalize
                // before combining, on top of the writer-side fix.
                .Select(f => f.Replace('\\', '/'))
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
record GalleryDef(string Slug, string Name, string Path, string? Password, bool ShowSummary, bool HideScreenshots, bool OnThisDayEnabled, bool SearchEnabled, bool HideAddons, bool CoreCategoriesOnly);
record LoginRequest(string Gallery, string Password);
record FolderEntry(string name, string relPath, string? cover, int row = 99, string? icon = null);
