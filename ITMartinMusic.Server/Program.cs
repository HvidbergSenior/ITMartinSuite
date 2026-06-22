using ITMartinMusic.Server.Data;
using ITMartinMusic.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<MusicDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("MusicDb")
        ?? "Data Source=/app/data/musik.db"));

builder.Services.AddScoped<MusicLibraryService>();
builder.Services.AddScoped<ChordService>();
builder.Services.AddScoped<AiCoachService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
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
        ".m4v"  => "video/mp4",
        ".webm" => "video/webm",
        _       => "application/octet-stream"
    };

    return Results.File(full, mime, enableRangeProcessing: true);
});

app.MapRazorComponents<ITMartinMusic.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
