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

builder.Services.AddScoped<StudioLibraryService>();
builder.Services.AddSingleton<ChordAiService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
    db.Database.EnsureCreated();
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
        ".webm" => "video/webm",
        _       => "application/octet-stream"
    };

    return Results.File(full, mime, enableRangeProcessing: true);
});

// Save a recording take to /musik/recordings/{songKey}/take-{timestamp}.webm
// Optionally promote to /musik/myversions/{songKey}.webm
app.MapPost("/api/recording/{songKey}", async (string songKey, HttpRequest req, IConfiguration cfg) =>
{
    var root = cfg["MusicSettings:Root"] ?? "/musik";
    var dir = Path.Combine(root, "recordings", songKey);
    Directory.CreateDirectory(dir);

    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    var dest = Path.Combine(dir, $"take-{timestamp}.webm");

    await using var stream = File.Create(dest);
    await req.Body.CopyToAsync(stream);

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

app.MapRazorComponents<ITMartinMusikStudio.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();

record PublishRequest(string RelativePath);
