using ITMartin.Ai;
using ITMartin.Ai.Interfaces;
using ITMartinMusicCheck.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MusicClearanceService>();
builder.Services.AddAi();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/api/check", async (string title, string? artist, MusicClearanceService svc) =>
{
    if (string.IsNullOrWhiteSpace(title)) return Results.BadRequest();
    var matches = await svc.CheckAsync(title, artist ?? "");
    return Results.Ok(new { found = matches.Count > 0, matches });
});

// One or more photos of CD covers/tracklists in - every recognised CD's
// tracks out, each already run through the same clearance check as the
// manual search box. A missing/guessed tracklist just means fewer tracks
// get checked, never a false "cleared" result.
app.MapPost("/api/scan-cds", async (HttpRequest req, ICdRecognitionService recognizer, MusicClearanceService clearance) =>
{
    if (!req.HasFormContentType) return Results.BadRequest();
    var form = await req.ReadFormAsync();
    if (form.Files.Count == 0) return Results.BadRequest();

    var cdResults = new List<object>();

    foreach (var file in form.Files)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"cdscan-{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");
        try
        {
            await using (var stream = File.Create(tempPath))
                await file.CopyToAsync(stream);

            var recognized = await recognizer.AnalyzeAsync(tempPath);
            if (recognized is null) continue;

            foreach (var cd in recognized.Cds)
            {
                var trackResults = new List<object>();
                foreach (var track in cd.Tracks)
                {
                    var matches = await clearance.CheckAsync(track, cd.Artist);
                    trackResults.Add(new { title = track, found = matches.Count > 0, matches });
                }
                cdResults.Add(new { cd.Artist, cd.Album, Tracks = trackResults });
            }
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    return Results.Ok(new { cds = cdResults });
});

app.MapRazorComponents<ITMartinMusicCheck.Server.App>();

app.Run();
