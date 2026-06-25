using ITMartinMusicGame;
using ITMartinMusicGame.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 50 * 1024 * 1024);

builder.Services.AddSingleton<RoomService>();
builder.Services.AddSingleton<SongService>();
builder.Services.AddSingleton<ScoringService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

// Stream audio/video from NAS
app.MapGet("/song/{*path}", (string path, IConfiguration cfg) =>
{
    var root = cfg["MusicSettings:Root"] ?? "/musik";
    path = Uri.UnescapeDataString(path);
    var full = Path.GetFullPath(Path.Combine(root, path));
    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        return Results.NotFound();

    var mime = Path.GetExtension(full).ToLowerInvariant() switch
    {
        ".mp3" => "audio/mpeg", ".m4a" => "audio/mp4", ".wav" => "audio/wav",
        ".ogg" => "audio/ogg", ".flac" => "audio/flac", ".aac" => "audio/aac",
        ".mp4" => "video/mp4", ".mov" => "video/quicktime",
        ".webm" => "video/webm", ".mkv" => "video/x-matroska",
        _ => "application/octet-stream"
    };

    return Results.File(full, mime, enableRangeProcessing: true);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
