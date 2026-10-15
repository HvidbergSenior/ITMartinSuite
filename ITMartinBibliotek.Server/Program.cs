using ITMartin.Shared.UI.Kolibri;
using ITMartinBibliotek.Application;
using ITMartinBibliotek.Server;
using ITMartinBibliotek.Domain;
using ITMartinBibliotek.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Bibliotek";
    k.KolibriName = "Kolibri Syn";
    k.Family = "syn";
    k.Tagline = "Dine cd'er, dvd'er og blu-rays – på hylden og på skærmen.";
    k.About =
    [
        "Bibliotek er din egen samling af cd'er, dvd'er og blu-rays – på hylden og på skærmen.",
        "Musik spiller her i appen, også med skærmen slukket. Film og serier åbner i Jellyfin.",
    ];
    k.HowTo =
    [
        "Søg efter en kunstner, et album, en sang eller en film i feltet øverst.",
        "Musik: vælg en kunstner i listen A–Å, så et album. Tryk ▶ Afspil, eller tryk på en sang for at starte ved den.",
        "Afspilleren nederst – og telefonens låseskærm – styrer pause og næste nummer.",
        "Film og serier: tryk ▶ Afspil, så åbner de i Jellyfin. Ny skive: tryk Scan og hold stregkoden foran kameraet.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#121917";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBibliotekInfrastructure(builder.Configuration);
builder.Services.AddBibliotekApplication();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Bibliotek:KeysDir"] ?? "/app/data/keys"))
    .SetApplicationName("bibliotek");

var app = builder.Build();

app.Services.EnsureBibliotekSchema();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// PIN login: the admin PIN is the household, each customer PIN (Customers) sees only
// that customer's own catalog and music. The resolved Viewer rides along in ctx.Items.
var customers = app.Services.GetRequiredService<Customers>();
static Viewer ViewerOf(HttpContext ctx) => ctx.Items["viewer"] as Viewer ?? throw new InvalidOperationException("no viewer");

app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var open = path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/rip/", StringComparison.OrdinalIgnoreCase)   // own key check below
            || path.StartsWith("/om", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/hjaelp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/hjaelp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/manifest.webmanifest", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/kolibri-sw.js", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/kolibri/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    var viewer = customers.Resolve(ctx.Request.Cookies["bibliotek_auth"]);
    if (viewer is not null) ctx.Items["viewer"] = viewer;
    if (open || viewer is not null)
    {
        await next();
        return;
    }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/auth/login", (HttpContext ctx, [FromForm] string pin) =>
{
    if (customers.Resolve(pin) is not null)
    {
        ctx.Response.Cookies.Append("bibliotek_auth", pin, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(365)
        });
        return Results.Redirect("/");
    }
    return Results.Redirect("/login?error=1");
}).DisableAntiforgery();

app.MapGet("/api/auth/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("bibliotek_auth");
    return Results.Redirect("/login");
});

// The rip station on the PC reports here (ripstation.ps1). It has no browser
// session, so it authenticates with its own key instead of the PIN cookie.
var ripKey = app.Configuration["Bibliotek:RipKey"] ?? "";
bool RipKeyOk(HttpContext ctx) =>
    ripKey.Length >= 16 && ctx.Request.Headers.TryGetValue("X-Rip-Key", out var k) && k == ripKey;

app.MapPost("/api/rip/job", async (HttpContext ctx, RipJob job, IRipStatus store, IJellyfin jellyfin, ILogger<RipJob> log) =>
{
    if (!RipKeyOk(ctx)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(job.Id)) return Results.BadRequest();
    var wasDone = store.Jobs().Any(j => j.Id == job.Id && j.Status == "done");
    store.Upsert(job);
    if (job.Status == "done" && !wasDone)
    {
        try { await jellyfin.RefreshLibraryAsync(); }
        catch (Exception ex) { log.LogWarning(ex, "Jellyfin refresh after rip {Id} failed", job.Id); }
    }
    return Results.Ok();
}).DisableAntiforgery();

// Title lookup for a disc label (TMDB in Danish, so "Ørkenens Sønner" is found too).
app.MapGet("/api/rip/lookup", async (HttpContext ctx, string q, Catalog catalog) =>
{
    if (!RipKeyOk(ctx)) return Results.Unauthorized();
    var hits = await catalog.LookupTitleAsync(q, MediaKind.Film);
    return Results.Ok(hits.Select(h => new { h.Title, h.Year, Kind = h.Kind.ToString() }));
});

app.MapPost("/api/rip/heartbeat", (HttpContext ctx, IRipStatus store) =>
{
    if (!RipKeyOk(ctx)) return Results.Unauthorized();
    store.Heartbeat();
    return Results.Ok();
}).DisableAntiforgery();

// Bibliotek's own music player (player.js). Track lists and audio come from Jellyfin
// through the app, so the phone needs only the PIN cookie, never a Jellyfin login.
// The play id is an album, or several joined with '_' (a 2-cd set Jellyfin split in two).
app.MapGet("/api/afspil/album/{playId}", async (string playId, HttpContext ctx, Playback playback) =>
{
    if (!await playback.MayPlayAsync(ViewerOf(ctx), playId)) return Results.NotFound();
    var album = await playback.AlbumAsync(playId);
    return album is null ? Results.NotFound() : Results.Ok(album);
});

// Album + position instead of a track id: the first tap can start the audio at once, inside
// the tap itself, which is what lets an iPhone go on to the next tracks with the screen off.
app.MapGet("/api/afspil/album/{playId}/spor/{index:int}", async (string playId, int index, HttpContext ctx, Playback playback) =>
{
    if (!await playback.MayPlayAsync(ViewerOf(ctx), playId))
    {
        ctx.Response.StatusCode = 404;
        return;
    }
    try
    {
        using var res = await playback.OpenTrackAsync(playId, index, ctx.Request.Headers.Range.ToString(), ctx.RequestAborted);
        if (res is null) { ctx.Response.StatusCode = 404; return; }
        ctx.Response.StatusCode = (int)res.StatusCode;
        var h = res.Content.Headers;
        if (h.ContentType is { } type) ctx.Response.ContentType = type.ToString();
        if (h.ContentLength is { } length) ctx.Response.ContentLength = length;
        if (h.ContentRange is { } contentRange) ctx.Response.Headers.ContentRange = contentRange.ToString();
        ctx.Response.Headers.AcceptRanges = "bytes";
        await res.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
    }
    catch (OperationCanceledException) { }   // the phone moved on to another range or track
});

app.MapPost("/api/afspil/spillet/{trackId}", async (string trackId, HttpContext ctx, Playback playback, ILogger<Playback> log) =>
{
    try { await playback.MarkPlayedAsync(ViewerOf(ctx), trackId); }
    catch (Exception ex) { log.LogWarning(ex, "Marking {Track} played in Jellyfin failed", trackId); }
    return Results.Ok();
}).DisableAntiforgery();

// A customer's own printed guide (it holds their PIN and passwords, so it is served only to them,
// never from an open folder): <KundeDir>/<customer key>/guide.pdf, dropped there by Martin.
var kundeDir = app.Configuration["Bibliotek:KundeDir"] ?? "/app/data/kunder";
app.MapGet("/api/vejledning", (HttpContext ctx) =>
{
    var viewer = ViewerOf(ctx);
    if (viewer.IsAdmin) return Results.NotFound();
    var file = Path.Combine(kundeDir, viewer.Owner, "guide.pdf");
    return File.Exists(file)
        ? Results.File(file, "application/pdf", enableRangeProcessing: true)
        : Results.NotFound();
});

// A customer's own Rip CD: the free zip plus send-til.txt with their upload link, so the program
// asks nothing and sends every album straight into their library. Only to that customer (it holds the token).
app.MapGet("/api/ripcd", (HttpContext ctx, IWebHostEnvironment env) =>
{
    var viewer = ViewerOf(ctx);
    var customer = customers.Get(viewer.Owner);
    if (customer is null || customer.Upload == "") return Results.NotFound();

    var free = env.WebRootFileProvider.GetFileInfo("download/RipCD.zip");
    if (!free.Exists || free.PhysicalPath is null) return Results.NotFound();

    var ms = new MemoryStream();
    File.OpenRead(free.PhysicalPath).CopyTo(ms);
    using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
    {
        using var w = new StreamWriter(zip.CreateEntry("send-til.txt").Open(), new System.Text.UTF8Encoding(false));
        w.WriteLine("# Rip CD sender hver cd hertil - til " + (customer.Navn == "" ? customer.Key : customer.Navn) + "s samling i Bibliotek. Del ikke filen.");
        w.WriteLine(customer.Upload);
    }
    ms.Position = 0;
    return Results.File(ms, "application/zip", "RipCD.zip");
});

// The step-by-step guide was removed 2026-10-15 (Martin guides the customer himself); old links land on the collection.
app.MapGet("/kom-i-gang", () => Results.Redirect("/"));

app.UseAntiforgery();

app.MapKolibri();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
