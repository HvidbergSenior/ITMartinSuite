using ITMartin.Shared.UI.Kolibri;
using ITMartinBibliotek.Server;
using ITMartinBibliotek.Server.Data;
using ITMartinBibliotek.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Bibliotek";
    k.KolibriName = "Kolibri Syn";
    k.Family = "syn";
    k.Tagline = "Dine cd'er, dvd'er og blu-rays – på hylden og på skærmen.";
    k.About =
    [
        "Bibliotek er kataloget over din egen samling. Scan stregkoden, og skiven står på listen med cover, år og spor.",
        "Det der er rippet, kan afspilles direkte: musik spiller her i appen – også med skærmen slukket – og film åbner i Jellyfin. Resten ved du hvor står, så du køber ikke den samme film to gange.",
    ];
    k.HowTo =
    [
        "Tryk Scan og hold stregkoden foran kameraet.",
        "Ret titlen hvis opslaget tog fejl – eller skriv den ind selv.",
        "Musik: tryk ▶ Afspil på et album. Afspilleren nederst – og telefonens låseskærm – styrer pause og næste nummer, og hele albummet spiller færdigt.",
        "Film og serier: tryk ▶ Afspil, så åbner de i Jellyfin.",
    ];
    k.Version = "2026.09";
    k.ThemeColor = "#121917";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("BibliotekDb")
    ?? "Data Source=/app/data/bibliotek.db";

builder.Services.AddDbContextFactory<BibliotekDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<JellyfinClient>();
builder.Services.AddSingleton<MetadataLookup>();
builder.Services.AddSingleton<Customers>();
builder.Services.AddSingleton<JellyfinSync>();
builder.Services.AddHostedService<CustomerSyncService>();
builder.Services.AddSingleton<RipStatusStore>();

// Audio streams run as long as a track plays, so they get no overall timeout.
builder.Services.AddHttpClient("jellyfin-stream", c => c.Timeout = Timeout.InfiniteTimeSpan);

// MusicBrainz and Discogs reject requests without a descriptive User-Agent.
const string userAgent = "ITMartinBibliotek/1.0 (hvidbergsenior@gmail.com)";
foreach (var name in new[] { "jellyfin", "musicbrainz", "discogs", "upc", "tmdb" })
{
    builder.Services.AddHttpClient(name, c =>
    {
        c.Timeout = TimeSpan.FromSeconds(15);
        c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    });
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Bibliotek:KeysDir"] ?? "/app/data/keys"))
    .SetApplicationName("bibliotek");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BibliotekDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.EnsureCreated();
    // EnsureCreated never alters an existing file, so columns added later are added by hand.
    var conn = db.Database.GetDbConnection();
    conn.Open();
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Items') WHERE name = 'Collection'";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            db.Database.ExecuteSqlRaw("ALTER TABLE Items ADD COLUMN Collection TEXT NOT NULL DEFAULT ''");
    }
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Items') WHERE name = 'Owner'";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            db.Database.ExecuteSqlRaw("ALTER TABLE Items ADD COLUMN Owner TEXT NOT NULL DEFAULT ''");
    }
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Items_Owner ON Items (Owner)");
}

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

app.MapPost("/api/rip/job", async (HttpContext ctx, RipJob job, RipStatusStore store, JellyfinClient jellyfin, ILogger<RipStatusStore> log) =>
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
app.MapGet("/api/rip/lookup", async (HttpContext ctx, string q, MetadataLookup lookup) =>
{
    if (!RipKeyOk(ctx)) return Results.Unauthorized();
    var hits = await lookup.SearchTitleAsync(q, ITMartinBibliotek.Server.Data.Entities.MediaKind.Film);
    return Results.Ok(hits.Select(h => new { h.Title, h.Year, Kind = h.Kind.ToString() }));
});

app.MapPost("/api/rip/heartbeat", (HttpContext ctx, RipStatusStore store) =>
{
    if (!RipKeyOk(ctx)) return Results.Unauthorized();
    store.Heartbeat();
    return Results.Ok();
}).DisableAntiforgery();

// Bibliotek's own music player (player.js). Track lists and audio come from Jellyfin
// through the app, so the phone needs only the PIN cookie, never a Jellyfin login.
// A customer may only play albums from their own library - checked on every request,
// including each byte range, because the album id alone is guessable from Jellyfin.
async Task<bool> MayPlay(HttpContext ctx, string albumId, IDbContextFactory<BibliotekDbContext> dbf)
{
    if (!JellyfinClient.IsId(albumId)) return false;
    var viewer = ViewerOf(ctx);
    if (viewer.IsAdmin) return true;
    await using var db = await dbf.CreateDbContextAsync();
    return await db.Items.AnyAsync(i => i.JellyfinId == albumId && i.Owner == viewer.Owner);
}

app.MapGet("/api/afspil/album/{albumId}", async (string albumId, HttpContext ctx, JellyfinClient jellyfin, IDbContextFactory<BibliotekDbContext> dbf) =>
{
    if (!await MayPlay(ctx, albumId, dbf)) return Results.NotFound();
    var album = await jellyfin.AlbumAsync(albumId);
    return album is null ? Results.NotFound() : Results.Ok(album);
});

// Album + position instead of a track id: the first tap can start the audio at once, inside
// the tap itself, which is what lets an iPhone go on to the next tracks with the screen off.
app.MapGet("/api/afspil/album/{albumId}/spor/{index:int}", async (string albumId, int index, HttpContext ctx, JellyfinClient jellyfin, IDbContextFactory<BibliotekDbContext> dbf) =>
{
    var album = await MayPlay(ctx, albumId, dbf) ? await jellyfin.AlbumAsync(albumId) : null;
    if (album is null || index < 0 || index >= album.Tracks.Count)
    {
        ctx.Response.StatusCode = 404;
        return;
    }
    try
    {
        using var res = await jellyfin.OpenAudioAsync(album.Tracks[index].Id, ctx.Request.Headers.Range.ToString(), ctx.RequestAborted);
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

app.MapPost("/api/afspil/spillet/{trackId}", async (string trackId, HttpContext ctx, JellyfinClient jellyfin, ILogger<JellyfinClient> log) =>
{
    if (!JellyfinClient.IsId(trackId)) return Results.NotFound();
    // Played marks go to the household's Jellyfin account, so a customer's listening stays out of it.
    if (!ViewerOf(ctx).IsAdmin) return Results.Ok();
    try { await jellyfin.MarkPlayedAsync(trackId); }
    catch (Exception ex) { log.LogWarning(ex, "Marking {Track} played in Jellyfin failed", trackId); }
    return Results.Ok();
}).DisableAntiforgery();

app.UseAntiforgery();

app.MapKolibri();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
