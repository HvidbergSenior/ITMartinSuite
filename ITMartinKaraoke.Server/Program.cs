using ITMartinKaraoke.Server.Data;
using ITMartinKaraoke.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 30 * 1024 * 1024);

builder.Services.AddDbContext<KaraokeDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("KaraokeDb")
        ?? "Data Source=/app/data/karaoke.db"));

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("fal");
builder.Services.AddSingleton<SpotifyService>();
builder.Services.AddSingleton<LyricsService>();
builder.Services.AddSingleton<KaraokeLibraryService>();
builder.Services.AddSingleton<KaraokeAiService>();
builder.Services.AddSingleton<PosterService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KaraokeDbContext>();
    db.Database.EnsureCreated();

    if (app.Configuration.GetValue<bool>("Karaoke:SeedDemoData"))
        await ITMartinKaraoke.Server.Data.DemoSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var libraryRoot = app.Configuration["KaraokeSettings:LibraryRoot"] ?? "/karaoke-library";
var dataRoot = Path.GetDirectoryName(
    (app.Configuration.GetConnectionString("KaraokeDb") ?? "Data Source=/app/data/karaoke.db")
        .Replace("Data Source=", "")) ?? "/app/data";
var recordingsRoot = Path.Combine(dataRoot, "recordings");

// ── Local ripped-CD library ─────────────────────────────────────────────
// (Blazor Server pages call KaraokeLibraryService/KaraokeDbContext directly,
// in-process - the only thing that genuinely needs an HTTP endpoint is
// serving the actual audio bytes back to a <audio>/<video> element.)

app.MapGet("/library-stream", (string path) =>
{
    var full = Path.GetFullPath(Path.Combine(libraryRoot, path));
    if (!full.StartsWith(libraryRoot, StringComparison.OrdinalIgnoreCase)) return Results.BadRequest();
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

// Queue, session, AI tips and poster generation all happen through Razor
// pages calling KaraokeDbContext/services directly (Blazor Server runs
// server-side already - no need to bounce through HTTP to reach itself).

// ── Recordings ───────────────────────────────────────────────────────────
// Every phone/device recording a performance uploads its own file, tagged
// with a label ("stage" for the TV/main capture, or a person's name for a
// phone-as-extra-mic recording) - these are never live-mixed, just saved
// side by side so they can be combined by hand afterward if wanted.

app.MapPost("/api/recording/{queueEntryId:int}", async (int queueEntryId, string label, HttpRequest req, KaraokeDbContext db) =>
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

app.MapRazorComponents<ITMartinKaraoke.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
