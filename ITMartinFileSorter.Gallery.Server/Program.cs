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

// Load galleries from config (supports env var array syntax: Galleries__0__Slug etc.)
var galleries = app.Configuration
    .GetSection("Galleries")
    .GetChildren()
    .Select(s => new GalleryDef(
        Slug: s["Slug"] ?? "",
        Name: s["Name"] ?? "",
        Path: s["Path"] ?? ""))
    .Where(g => !string.IsNullOrWhiteSpace(g.Slug) && !string.IsNullOrWhiteSpace(g.Path))
    .ToList();

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

app.MapGet("/api/browse", (string gallery, string? path) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null) return Results.NotFound();
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
                name    = Path.GetFileName(f),
                relPath = Rel(f, r),
                webPath = wp,
                thumb   = IsImg(ext) ? (Thumb(f, r, g.Slug) ?? wp) : (string?)null,
                isVideo = IsVid(ext),
                isAudio = IsAud(ext),
            };
        })
        .ToList();

    var atRoot     = IsSameDir(current, r);
    var parentFull = atRoot ? null : Directory.GetParent(current)?.FullName;
    var parentRel  = parentFull is null ? null : NormalizeRel(Rel(parentFull, r));

    return Results.Ok(new { atRoot, parentRelPath = parentRel, folders, files });
});

app.MapGet("/api/playlist", (string gallery, string folder) =>
{
    var g = galleries.FirstOrDefault(x => x.Slug == gallery);
    if (g is null) return Results.NotFound();
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
                name    = Path.GetFileName(f),
                relPath = Rel(f, r),
                webPath = Web(f, r, g.Slug),
                isVideo = IsVid(ext),
                isAudio = IsAud(ext),
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

// Type declarations must come after all top-level statements and local functions
record GalleryDef(string Slug, string Name, string Path);
