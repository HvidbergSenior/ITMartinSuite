using ITMartinMusic.Server.Data;
using ITMartinMusic.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<MusicDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("MusicDb")
        ?? "Data Source=/app/data/musik.db"));

builder.Services.AddSingleton<MusicBroadcastService>();
builder.Services.AddScoped<MusicLibraryService>();
builder.Services.AddScoped<ChordService>();
builder.Services.AddScoped<AiCoachService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
    db.Database.EnsureCreated();
    // Safe for existing DBs — creates table only if missing
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS SongComments (
            Id        INTEGER PRIMARY KEY AUTOINCREMENT,
            SongKey   TEXT NOT NULL DEFAULT '',
            Name      TEXT NOT NULL DEFAULT '',
            Text      TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL DEFAULT ''
        )");
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
        ".m4v"  => "video/mp4",
        ".webm" => "video/webm",
        ".jpg"  => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".png"  => "image/png",
        ".gif"  => "image/gif",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        _       => "application/octet-stream"
    };

    return Results.File(full, mime, enableRangeProcessing: true);
});

app.MapPost("/upload-lyrics", async (HttpRequest req, IConfiguration cfg) =>
{
    var root = cfg["MusicSettings:Root"] ?? "/musik";
    var lyricsDir = Path.Combine(root, "lyrics");
    Directory.CreateDirectory(lyricsDir);

    var form = await req.ReadFormAsync();
    var file = form.Files.GetFile("file");
    var key  = form["key"].ToString();

    if (file is null || string.IsNullOrWhiteSpace(key))
        return Results.BadRequest();

    var ext  = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic" }.Contains(ext))
        return Results.BadRequest("Not an image");

    var dest = Path.Combine(lyricsDir, key + ext);
    await using var stream = File.Create(dest);
    await file.CopyToAsync(stream);

    return Results.Ok(new { path = $"lyrics/{key}{ext}" });
});

app.MapRazorComponents<ITMartinMusic.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
