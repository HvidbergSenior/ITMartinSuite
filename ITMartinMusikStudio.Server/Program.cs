using ITMartinMusikStudio.Server.Data;
using ITMartinMusikStudio.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 30 * 1024 * 1024);

builder.Services.AddDbContext<StudioDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("StudioDb")
        ?? "Data Source=/app/data/studio.db"));

builder.Services.AddHttpClient("fal");
builder.Services.AddScoped<StudioLibraryService>();
builder.Services.AddScoped<CoverArtService>();
builder.Services.AddSingleton<ChordAiService>();
builder.Services.AddSingleton<StemService>();
builder.Services.AddSingleton<ChordDetectionService>();
builder.Services.AddSingleton<PianoTranscriptionService>();
builder.Services.AddSingleton<VocalGuideService>();
builder.Services.AddSingleton<SpotifyService>();
builder.Services.AddSingleton<LyricsService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
    db.Database.EnsureCreated();
    // Add columns introduced after initial schema — safe to re-run (SQLite ignores duplicate column errors)
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN FingerpickPattern TEXT NOT NULL DEFAULT ''"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN StrumPattern TEXT NOT NULL DEFAULT ''"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN Artist TEXT NOT NULL DEFAULT ''"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN SpotifyTrackId TEXT NULL"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN SpotifyTrackLabel TEXT NULL"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN SyncedLyrics TEXT NULL"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN SkippedSteps TEXT NOT NULL DEFAULT ''"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN CoverImagePath TEXT NOT NULL DEFAULT ''"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN SectionTimings TEXT NULL"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN LineBeats TEXT NULL"); } catch { }
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var musikRoot = app.Configuration["MusicSettings:Root"] ?? "/musik";

app.MapGet("/stream", (string path, HttpContext ctx) =>
{
    var full = Path.GetFullPath(Path.Combine(musikRoot, path));
    if (!full.StartsWith(musikRoot, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest();
    if (!File.Exists(full))
        return Results.NotFound();

    var isAudioOnlyTake = Path.GetFileNameWithoutExtension(full).StartsWith("take-", StringComparison.OrdinalIgnoreCase);
    var mime = Path.GetExtension(full).ToLowerInvariant() switch
    {
        ".mp3"  => "audio/mpeg",
        ".m4a"  => "audio/mp4",
        ".wav"  => "audio/wav",
        ".ogg"  => "audio/ogg",
        ".flac" => "audio/flac",
        ".aac"  => "audio/aac",
        ".mp4"  => "video/mp4",
        ".mov"  => "video/quicktime",
        ".webm" => isAudioOnlyTake ? "audio/webm" : "video/webm",
        ".jpg"  => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".png"  => "image/png",
        ".webp" => "image/webp",
        _       => "application/octet-stream"
    };

    return Results.File(full, mime, enableRangeProcessing: true);
});

// Save a recording take to /musik/recordings/{songKey}/take-{timestamp}.webm
// Optionally promote to /musik/myversions/{songKey}.webm
app.MapPost("/api/recording/{songKey}", async (string songKey, HttpRequest req, IConfiguration cfg) =>
{
    // Kestrel's default 30MB request body cap was silently truncating longer
    // video takes mid-upload - the partial file still landed on disk and
    // showed up in the takes list, but was corrupt and wouldn't play. Raise
    // it well past what a single take can realistically reach.
    var sizeFeature = req.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
    if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = 500 * 1024 * 1024;

    var root = cfg["MusicSettings:Root"] ?? "/musik";
    var dir = Path.Combine(root, "recordings", songKey);
    Directory.CreateDirectory(dir);

    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    var prefix = req.ContentType?.Contains("video") == true ? "vtake" : "take";

    // Optional "record one section at a time" tag (see the Optag tab's
    // section picker) - embedded straight into the filename rather than a
    // side-table, same idiom as the take-/vtake-/aitake-/mixtake- prefixes
    // GetRecordings() already parses back out.
    var section = req.Query["section"].ToString();
    var sectionTag = string.IsNullOrWhiteSpace(section)
        ? ""
        : "-" + new string(section.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    var dest = Path.Combine(dir, $"{prefix}{sectionTag}-{timestamp}.webm");

    try
    {
        await using (var stream = File.Create(dest))
        {
            await req.Body.CopyToAsync(stream);
        }
    }
    catch
    {
        // Don't leave a truncated, unplayable file behind that would still
        // show up as a take in the list.
        if (File.Exists(dest)) File.Delete(dest);
        throw;
    }

    var rel = Path.GetRelativePath(root, dest).Replace('\\', '/');
    return Results.Ok(new { path = rel, filename = Path.GetFileName(dest) });
});

// Promote a take to myversions (publish to public app)
app.MapPost("/api/publish/{songKey}", async (string songKey, PublishRequest body, IConfiguration cfg) =>
{
    var root = cfg["MusicSettings:Root"] ?? "/musik";
    var src = Path.GetFullPath(Path.Combine(root, body.RelativePath));
    if (!src.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(src))
        return Results.NotFound();

    var mvDir = Path.Combine(root, "myversions");
    Directory.CreateDirectory(mvDir);
    var dest = Path.Combine(mvDir, $"{songKey}.webm");
    File.Copy(src, dest, overwrite: true);

    return Results.Ok();
});

// Delete a recording take
app.MapDelete("/api/recording", (string path, IConfiguration cfg) =>
{
    var root = cfg["MusicSettings:Root"] ?? "/musik";
    var full = Path.GetFullPath(Path.Combine(root, path));
    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return Results.BadRequest();
    if (File.Exists(full)) File.Delete(full);
    return Results.Ok();
});

// Save a short "hum an idea" sketch clip for the Skriv sang flow - parallel
// to /api/recording but a distinct, much smaller-scoped concept: not a take,
// never mixed into the take list, just a quick scratch clip meant to be
// downloaded and handed to Suno externally. No 500MB body-size override like
// /api/recording needs - sketches are seconds long.
app.MapPost("/api/sketch/{songKey}", async (string songKey, HttpRequest req, IConfiguration cfg) =>
{
    var root = cfg["MusicSettings:Root"] ?? "/musik";
    var dir = Path.Combine(root, "sketches", songKey);
    Directory.CreateDirectory(dir);
    var ext = req.ContentType?.Contains("webm") == true ? ".webm" : ".ogg";
    var dest = Path.Combine(dir, $"sketch-{DateTime.UtcNow:yyyyMMdd-HHmmss}{ext}");
    try
    {
        await using var stream = File.Create(dest);
        await req.Body.CopyToAsync(stream);
    }
    catch
    {
        if (File.Exists(dest)) File.Delete(dest);
        throw;
    }
    var rel = Path.GetRelativePath(root, dest).Replace('\\', '/');
    return Results.Ok(new { path = rel });
});

// Delete a sketch - same path-under-root convention as /api/recording DELETE
app.MapDelete("/api/sketch", (string path, IConfiguration cfg) =>
{
    var root = cfg["MusicSettings:Root"] ?? "/musik";
    var full = Path.GetFullPath(Path.Combine(root, path));
    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return Results.BadRequest();
    if (File.Exists(full)) File.Delete(full);
    return Results.Ok();
});

// Forces a browser download instead of inline playback - /stream never sets
// Content-Disposition: attachment, so there was previously no way to get a
// file out of this app onto the user's own machine (needed to hand a sketch
// clip to Suno).
app.MapGet("/download", (string path, IConfiguration cfg) =>
{
    var root = cfg["MusicSettings:Root"] ?? "/musik";
    var full = Path.GetFullPath(Path.Combine(root, path));
    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return Results.BadRequest();
    if (!File.Exists(full)) return Results.NotFound();
    var mime = Path.GetExtension(full).ToLowerInvariant() switch
    {
        ".webm" => "audio/webm",
        ".ogg"  => "audio/ogg",
        ".mp3"  => "audio/mpeg",
        _       => "application/octet-stream"
    };
    return Results.File(full, mime, fileDownloadName: Path.GetFileName(full));
});

// ── Spotify ───────────────────────────────────────────────────────────────

app.MapGet("/spotify/login", (SpotifyService spotify) =>
{
    if (!spotify.IsConfigured) return Results.Problem("Spotify:ClientId/ClientSecret not configured");
    return Results.Redirect(spotify.GetAuthorizeUrl(state: Guid.NewGuid().ToString("N")));
});

app.MapGet("/spotify/callback", async (string? code, string? error, SpotifyService spotify) =>
{
    if (!string.IsNullOrEmpty(error)) return Results.Redirect("/?spotify=error");
    if (string.IsNullOrEmpty(code)) return Results.BadRequest();

    var ok = await spotify.HandleCallbackAsync(code);
    return Results.Redirect(ok ? "/?spotify=connected" : "/?spotify=error");
});

// The Web Playback SDK (client-side JS) needs a bearer token to hand to
// Spotify.Player - this is that token, refreshed transparently server-side.
app.MapGet("/api/spotify/token", async (SpotifyService spotify) =>
{
    var token = await spotify.GetValidAccessTokenAsync();
    return token is null ? Results.Unauthorized() : Results.Ok(new { accessToken = token });
});

app.MapGet("/api/spotify/search", async (string q, SpotifyService spotify) =>
    Results.Ok(await spotify.SearchTracksAsync(q)));

app.MapRazorComponents<ITMartinMusikStudio.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();

record PublishRequest(string RelativePath);
