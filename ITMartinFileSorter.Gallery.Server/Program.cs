using System.Text.Json;
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
mime.Mappings[".flac"] = "audio/flac";
mime.Mappings[".ogg"]  = "audio/ogg";

var galleries = app.Configuration
    .GetSection("Galleries")
    .GetChildren()
    .Select(s => new GalleryDef(
        Slug:     s["Slug"]     ?? "",
        Name:     s["Name"]     ?? "",
        Path:     s["Path"]     ?? "",
        Password: s["Password"]))
    .Where(g => !string.IsNullOrWhiteSpace(g.Slug) && !string.IsNullOrWhiteSpace(g.Path))
    .ToList();

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

app.MapGet("/api/galleries", () =>
    galleries.Select(g => new { g.Slug, g.Name }));

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

    var r       = g.Path;
    var current = string.IsNullOrWhiteSpace(path) ? r : Path.GetFullPath(Path.Combine(r, path));

    if (!Directory.Exists(current)) return Results.NotFound();
    if (!IsSafe(current, r))        return Results.BadRequest("path outside library");

    var folders = Directory.EnumerateDirectories(current)
        .Where(d => !Path.GetFileName(d).StartsWith('.'))
        .OrderBy(Path.GetFileName)
        .Select(d => new
        {
            name    = Path.GetFileName(d),
            relPath = Rel(d, r),
            cover   = FolderCover(d, r, g.Slug),
        })
        .ToList();

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
                thumb     = IsImg(ext) ? (Thumb(f, r, g.Slug) ?? wp) : (string?)null,
                isVideo   = IsVid(ext),
                isAudio   = IsAud(ext),
                liveVideo = FindLivePhotoVideo(f, r, g.Slug),
            };
        })
        .ToList();

    var atRoot     = IsSameDir(current, r);
    var parentFull = atRoot ? null : Directory.GetParent(current)?.FullName;
    var parentRel  = parentFull is null ? null : NormalizeRel(Rel(parentFull, r));

    return Results.Ok(new { atRoot, parentRelPath = parentRel, folders, files });
});

app.MapGet("/api/summary", (string gallery, HttpContext ctx) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null) return Results.NotFound();
    if (!string.IsNullOrEmpty(g.Password) &&
        ctx.Request.Cookies[$"gallery_{gallery}"] != g.Password)
        return Results.Unauthorized();

    if (!Directory.Exists(g.Path)) return Results.NotFound();

    var mediaFiles = Directory.EnumerateFiles(g.Path, "*.*", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}thumbnails{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(IsMedia);

    int photoCount = 0, videoCount = 0, audioCount = 0, totalCount = 0;
    long totalBytes = 0;
    DateTime? earliest = null, latest = null;

    foreach (var f in mediaFiles)
    {
        var ext = Ext(f);
        if (IsImg(ext)) photoCount++;
        else if (IsVid(ext)) videoCount++;
        else if (IsAud(ext)) audioCount++;
        totalCount++;

        var fi = new FileInfo(f);
        totalBytes += fi.Length;
        var t = fi.LastWriteTimeUtc;
        if (earliest is null || t < earliest) earliest = t;
        if (latest is null || t > latest) latest = t;
    }

    var folderCount = Directory.EnumerateDirectories(g.Path)
        .Count(d => !Path.GetFileName(d).StartsWith('.') &&
                    !Path.GetFileName(d).Equals("thumbnails", StringComparison.OrdinalIgnoreCase));

    return Results.Ok(new
    {
        totalCount, photoCount, videoCount, audioCount, folderCount,
        earliestDate = earliest,
        latestDate   = latest,
        totalGb      = Math.Round(totalBytes / 1024.0 / 1024.0 / 1024.0, 1),
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

    var summaries = list
        .Where(c => c.FilePaths.Count > 0)
        .Select(c => new
        {
            name      = c.Name,
            fileCount = c.FilePaths.Count,
            cover     = c.FilePaths.Select(f => TryWeb(f, g.Path, g.Slug)).FirstOrDefault(w => w is not null),
        })
        .OrderByDescending(c => c.fileCount)
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

    var files = col.FilePaths
        .Where(f => File.Exists(f) && IsSafe(f, g.Path))
        .Select(f =>
        {
            var ext = Ext(f);
            var wp  = Web(f, g.Path, g.Slug);
            return new
            {
                name      = Path.GetFileName(f),
                relPath   = Rel(f, g.Path),
                webPath   = wp,
                thumb     = IsImg(ext) ? (Thumb(f, g.Path, g.Slug) ?? wp) : (string?)null,
                isVideo   = IsVid(ext),
                isAudio   = IsAud(ext),
                liveVideo = FindLivePhotoVideo(f, g.Path, g.Slug),
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

static string? Thumb(string f, string r, string slug)
{
    var t = Path.Combine(Path.GetDirectoryName(f)!, "thumbnails", Path.GetFileNameWithoutExtension(f) + ".jpg");
    return File.Exists(t) ? Web(t, r, slug) : null;
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

static string? FolderCover(string dir, string r, string slug)
{
    var td = Path.Combine(dir, "thumbnails");
    if (Directory.Exists(td))
    {
        var t = Directory.EnumerateFiles(td, "*.jpg").FirstOrDefault();
        if (t is not null) return Web(t, r, slug);
    }
    var img = Directory.EnumerateFiles(dir).FirstOrDefault(f => IsImg(Ext(f)));
    return img is not null ? Web(img, r, slug) : null;
}

static List<MediaCollection> LoadCollections(string libraryPath)
{
    var path = Path.Combine(libraryPath, "collections.json");
    if (!File.Exists(path)) return [];
    try
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<MediaCollection>>(json) ?? [];
    }
    catch (JsonException)
    {
        return [];
    }
}

static string? TryWeb(string f, string r, string slug) =>
    File.Exists(f) && IsSafe(f, r) ? Web(f, r, slug) : null;

record GalleryDef(string Slug, string Name, string Path, string? Password);
record LoginRequest(string Gallery, string Password);
