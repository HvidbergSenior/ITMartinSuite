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
        "Det der er rippet, kan afspilles direkte i Jellyfin – resten ved du hvor står. Så køber du ikke den samme film to gange.",
    ];
    k.HowTo =
    [
        "Tryk Scan og hold stregkoden foran kameraet.",
        "Ret titlen hvis opslaget tog fejl – eller skriv den ind selv.",
        "Tryk Jellyfin for at se eller høre det der er rippet.",
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
builder.Services.AddSingleton<JellyfinSync>();
builder.Services.AddSingleton<RipStatusStore>();

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
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// Single-PIN login: the catalog is for the household, sharing of the media
// itself happens inside Jellyfin's own user accounts.
var adminPin = app.Configuration["Bibliotek:AdminPin"] ?? "bibliotek2026";

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
    if (open || (ctx.Request.Cookies.TryGetValue("bibliotek_auth", out var v) && v == adminPin))
    {
        await next();
        return;
    }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/auth/login", (HttpContext ctx, [FromForm] string pin) =>
{
    if (pin == adminPin)
    {
        ctx.Response.Cookies.Append("bibliotek_auth", adminPin, new CookieOptions
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

app.UseAntiforgery();

app.MapKolibri();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
