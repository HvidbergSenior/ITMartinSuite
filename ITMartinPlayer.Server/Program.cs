using ITMartinPlayer.Server.Data;
using ITMartinPlayer.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 30 * 1024 * 1024);

builder.Services.AddDbContext<PlayerDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("PlayerDb")
        ?? "Data Source=/app/data/player.db"));

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("fal");
builder.Services.AddSingleton<SpotifyService>();
builder.Services.AddSingleton<LyricsService>();
builder.Services.AddSingleton<MusicLibraryService>();
builder.Services.AddSingleton<KaraokeAiService>();
builder.Services.AddSingleton<PosterService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlayerDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var libraryRoot = app.Configuration["PlayerSettings:LibraryRoot"] ?? "/music-library";
var dataRoot = Path.GetDirectoryName(
    (app.Configuration.GetConnectionString("PlayerDb") ?? "Data Source=/app/data/player.db")
        .Replace("Data Source=", "")) ?? "/app/data";
var recordingsRoot = Path.Combine(dataRoot, "recordings");

// ── Music library streaming ─────────────────────────────────────────────
// (Blazor Server pages call MusicLibraryService/PlayerDbContext directly,
// in-process - the only thing that genuinely needs an HTTP endpoint is
// serving the actual audio bytes back to the <audio> element.)

app.MapGet("/library-stream", (string path) =>
{
    var full = Path.GetFullPath(Path.Combine(libraryRoot, path));
    if (!full.StartsWith(Path.GetFullPath(libraryRoot), StringComparison.OrdinalIgnoreCase)) return Results.BadRequest();
    if (!File.Exists(full)) return Results.NotFound();

    var mime = Path.GetExtension(full).ToLowerInvariant() switch
    {
        ".mp3" => "audio/mpeg",
        ".m4a" => "audio/mp4",
        ".wav" => "audio/wav",
        ".ogg" => "audio/ogg",
        ".flac" => "audio/flac",
        ".aac" => "audio/aac",
        _ => "application/octet-stream"
    };
    return Results.File(full, mime, enableRangeProcessing: true);
});

// Only reached for albums with no folder-level cover image (Folder.jpg etc.)
// - most of this library already has one, so this per-request ID3 decode
// only runs for the minority that don't.
app.MapGet("/api/embedded-cover", (string path) =>
{
    var full = Path.GetFullPath(Path.Combine(libraryRoot, path));
    if (!full.StartsWith(Path.GetFullPath(libraryRoot), StringComparison.OrdinalIgnoreCase)) return Results.BadRequest();
    if (!File.Exists(full)) return Results.NotFound();

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

app.MapGet("/api/lyrics", async (string title, string artist, int? durationMs, LyricsService lyrics) =>
{
    var (synced, plain) = await lyrics.FindLyricsAsync(title, artist, durationMs);
    if (string.IsNullOrWhiteSpace(synced) && string.IsNullOrWhiteSpace(plain)) return Results.Ok(new { lines = Array.Empty<object>(), plain = "" });

    var lines = LyricsService.ParseLrc(synced).Select(l => new { t = l.Seconds, text = l.Text });
    return Results.Ok(new { lines, plain });
});

// ── Recordings ───────────────────────────────────────────────────────────
// Every phone/device recording a performance uploads its own file, tagged
// with a label ("stage" for the TV/main capture, or a person's name for a
// phone-as-extra-mic recording) - these are never live-mixed, just saved
// side by side so they can be combined by hand afterward if wanted.

app.MapPost("/api/recording/{queueEntryId:int}", async (int queueEntryId, string label, HttpRequest req, PlayerDbContext db) =>
{
    var entry = await db.QueueEntries.FindAsync(queueEntryId);
    if (entry is null) return Results.NotFound();

    var safeLabel = string.Concat((label ?? "gæst").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
    if (string.IsNullOrWhiteSpace(safeLabel)) safeLabel = "gæst";

    var dir = Path.Combine(recordingsRoot, queueEntryId.ToString());
    Directory.CreateDirectory(dir);

    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    var isVideo = req.ContentType?.Contains("video") == true;
    var prefix = isVideo ? "vtake" : "take";
    var dest = Path.Combine(dir, $"{prefix}-{safeLabel}-{timestamp}.webm");

    await using (var stream = File.Create(dest))
        await req.Body.CopyToAsync(stream);

    if (safeLabel == "stage")
    {
        entry.RecordingFile = Path.GetRelativePath(dataRoot, dest).Replace('\\', '/');
        await db.SaveChangesAsync();
    }

    return Results.Ok(new { filename = Path.GetFileName(dest), isVideo });
});

// Recordings.razor queries QueueEntries directly (same process, no need for
// an HTTP round trip) - this endpoint just serves the actual audio/video
// bytes back to the browser, which a Blazor Server component can't do itself.
app.MapGet("/recording-stream", (string path) =>
{
    var full = Path.GetFullPath(Path.Combine(dataRoot, path));
    if (!full.StartsWith(recordingsRoot, StringComparison.OrdinalIgnoreCase)) return Results.BadRequest();
    if (!File.Exists(full)) return Results.NotFound();
    var isVideo = Path.GetFileNameWithoutExtension(full).StartsWith("vtake", StringComparison.OrdinalIgnoreCase);
    return Results.File(full, isVideo ? "video/webm" : "audio/webm", enableRangeProcessing: true);
});

// ── Spotify ──────────────────────────────────────────────────────────────

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

app.MapGet("/api/spotify/token", async (SpotifyService spotify) =>
{
    var token = await spotify.GetValidAccessTokenAsync();
    return token is null ? Results.Unauthorized() : Results.Ok(new { accessToken = token });
});

app.MapRazorComponents<ITMartinPlayer.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
