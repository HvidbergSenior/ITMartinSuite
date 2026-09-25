using ITMartin.Shared.UI.Kolibri;
using ITMartinStrategi.Server;
using ITMartinStrategi.Server.Data;
using ITMartinStrategi.Server.Data.Entities;
using ITMartinStrategi.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Strategy";
    k.Language = "en";
    k.KolibriName = "Kolibri Navigate";
    k.Family = "navigere";
    k.Tagline = "Understand the game and play better – without cheating.";
    k.About =
    [
        "Strategy collects guides for the games you play: how the mechanics work, what to do first, and what to do once you have chosen something – a government, a leader, a strategy.",
        "Guides are drafted for the current version of the game. You adjust them and write your own strategies alongside.",
    ];
    k.HowTo =
    [
        "Pick a game.",
        "Pick a topic to write a guide – or write your own strategy.",
        "Edit the draft and mark it as checked when you agree.",
        "Read what is new in the game and your next achievements on the game page.",
    ];
    k.Version = "2026.09";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("StrategiDb") ?? "Data Source=/app/data/strategi.db";
builder.Services.AddDbContextFactory<StrategiDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddSingleton<StrategiAi>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("steam", c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton<SteamService>();
builder.Services.AddHttpClient("wiki", c => { c.Timeout = TimeSpan.FromSeconds(30); c.DefaultRequestHeaders.UserAgent.ParseAdd("ITMartinStrategi/1.0 (ITMartin@Mensa.dk)"); });
builder.Services.AddSingleton<WikiLibrary>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WikiLibrary>());

var app = builder.Build();
app.MapKolibri();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<StrategiDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    // EnsureCreated does not alter existing tables: add columns introduced later.
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Games ADD COLUMN Links TEXT NOT NULL DEFAULT ''"); }
    catch (Microsoft.Data.Sqlite.SqliteException e) when (e.Message.Contains("duplicate column")) { }
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Games ADD COLUMN SteamAppId INTEGER NULL"); }
    catch (Microsoft.Data.Sqlite.SqliteException e) when (e.Message.Contains("duplicate column")) { }
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Games ADD COLUMN Wiki TEXT NOT NULL DEFAULT ''"); }
    catch (Microsoft.Data.Sqlite.SqliteException e) when (e.Message.Contains("duplicate column")) { }
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Games ADD COLUMN WikiUpdatedAt TEXT NULL"); }
    catch (Microsoft.Data.Sqlite.SqliteException e) when (e.Message.Contains("duplicate column")) { }
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS WikiEntries (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, GameId INTEGER NOT NULL, Title TEXT NOT NULL, Kind TEXT NOT NULL,
            Text TEXT NOT NULL, Url TEXT NOT NULL, Icon TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS IX_WikiEntries_GameId ON WikiEntries (GameId);
        CREATE TABLE IF NOT EXISTS ItemAdvice (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, GameId INTEGER NOT NULL, Title TEXT NOT NULL, "Group" TEXT NOT NULL,
            Purpose TEXT NOT NULL, UseOn TEXT NOT NULL, AvoidOn TEXT NOT NULL, Example TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_ItemAdvice_GameId_Title ON ItemAdvice (GameId, Title);
        """);
    await Seed.EnsureAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

// Single-PIN login: the guides are Martin's own notebook.
var pin = app.Configuration["Strategi:Pin"] ?? "strategi2026";
string[] openPrefixes =
[
    "/_blazor", "/_framework", "/login", "/api/auth", "/kolibri/", "/om", "/hjaelp", "/api/hjaelp",
    "/health", "/manifest.webmanifest", "/kolibri-sw.js", "/_content/",
];
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var open = openPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
               || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    if (open || (ctx.Request.Cookies.TryGetValue("strategi_auth", out var v) && v == pin))
    {
        await next();
        return;
    }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/auth/login", (HttpContext ctx, [FromForm] string pin_) =>
{
    if (pin_ != pin) return Results.Redirect("/login?error=1");
    ctx.Response.Cookies.Append("strategi_auth", pin, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromDays(365),
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/api/auth/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("strategi_auth");
    return Results.Redirect("/login");
});

// Guides written elsewhere (in a Claude Code session, at no API cost) are
// posted here as JSON. Behind the PIN gate like every other page.
app.MapPost("/api/guides", async (List<ImportGuide> items, IDbContextFactory<StrategiDbContext> factory) =>
{
    await using var db = await factory.CreateDbContextAsync();
    var games = await db.Games.ToDictionaryAsync(g => g.Slug);
    var added = new List<int>();
    foreach (var i in items)
    {
        if (!games.TryGetValue(i.Game, out var game) || !Sections.All.Contains(i.Section)
            || string.IsNullOrWhiteSpace(i.Title) || string.IsNullOrWhiteSpace(i.Body)) continue;
        var g = new Guide
        {
            GameId = game.Id, Section = i.Section, Title = i.Title.Trim(), Trigger = (i.Trigger ?? "").Trim(),
            Body = i.Body.Trim(), Source = string.IsNullOrWhiteSpace(i.Source) ? "AI" : i.Source.Trim(),
            SourceUrls = string.Join('\n', i.Sources ?? []),
        };
        db.Guides.Add(g);
        await db.SaveChangesAsync();
        added.Add(g.Id);
    }
    return Results.Ok(new { added = added.Count, ids = added });
}).DisableAntiforgery();

// Micromanagement advice, written in a Claude Code session like the guides.
// Upsert by (game, title) so a corrected batch can simply be posted again.
app.MapPost("/api/advice", async (List<ImportAdvice> items, IDbContextFactory<StrategiDbContext> factory) =>
{
    await using var db = await factory.CreateDbContextAsync();
    var games = await db.Games.ToDictionaryAsync(g => g.Slug);
    var saved = 0;
    foreach (var i in items)
    {
        if (!games.TryGetValue(i.Game, out var game) || string.IsNullOrWhiteSpace(i.Title)) continue;
        var title = i.Title.Trim();
        var a = await db.Advice.FirstOrDefaultAsync(x => x.GameId == game.Id && x.Title == title);
        if (a is null) db.Advice.Add(a = new ItemAdvice { GameId = game.Id, Title = title });
        a.Group = i.Group.Trim();
        a.Purpose = i.Purpose.Trim();
        a.UseOn = i.UseOn.Trim();
        a.AvoidOn = i.AvoidOn.Trim();
        a.Example = (i.Example ?? "").Trim();
        a.UpdatedAt = DateTime.UtcNow;
        saved++;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { saved });
}).DisableAntiforgery();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public sealed record ImportAdvice(string Game, string Title, string Group, string Purpose, string UseOn, string AvoidOn, string? Example);
public sealed record ImportGuide(string Game, string Section, string Title, string? Trigger, string Body, string? Source, List<string>? Sources);
