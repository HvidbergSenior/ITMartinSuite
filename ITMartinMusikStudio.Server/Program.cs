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
    // The db lives on the NAS and is opened from Windows over SMB as well as
    // from containers on the NAS itself. SQLite WAL mode does not work across
    // a network share (each side keeps its own WAL index - reads go stale and
    // updates hit 0 rows, and on 2026-09-17 the file ended up "malformed").
    // The classic rollback journal uses plain file locks, which SMB honours.
    try { db.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE"); } catch { }
    // Add columns introduced after the initial schema. Only the ones actually
    // missing - a failed ALTER used to be logged as a red EF "fail" on every
    // start, which looked like a real error.
    var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    using (var cmd = db.Database.GetDbConnection().CreateCommand())
    {
        db.Database.OpenConnection();
        cmd.CommandText = "PRAGMA table_info(Songs)";
        using var r = cmd.ExecuteReader();
        while (r.Read()) existing.Add(r.GetString(1));
    }
    void AddColumn(string name, string ddl) { if (!existing.Contains(name)) db.Database.ExecuteSqlRaw($"ALTER TABLE Songs ADD COLUMN {name} {ddl}"); }
    AddColumn("FingerpickPattern", "TEXT NOT NULL DEFAULT ''");
    AddColumn("StrumPattern", "TEXT NOT NULL DEFAULT ''");
    AddColumn("Artist", "TEXT NOT NULL DEFAULT ''");
    AddColumn("SpotifyTrackId", "TEXT NULL");
    AddColumn("SpotifyTrackLabel", "TEXT NULL");
    AddColumn("SyncedLyrics", "TEXT NULL");
    AddColumn("SkippedSteps", "TEXT NOT NULL DEFAULT ''");
    AddColumn("CoverImagePath", "TEXT NOT NULL DEFAULT ''");
    AddColumn("SectionTimings", "TEXT NULL");
    AddColumn("LineBeats", "TEXT NULL");

    if (app.Configuration.GetValue<bool>("MusikStudio:SeedDemoData"))
        await ITMartinMusikStudio.Server.Data.DemoSeeder.SeedAsync(db);
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

// Forces a browser download instead of inline playback - /stream never sets
// Content-Disposition: attachment, so there was previously no way to get a
// file out of this app onto the user's own machine (e.g. to hand a take
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
