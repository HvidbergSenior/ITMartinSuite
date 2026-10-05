using ITMartin.Ai;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Services;
using ITMartin.Magic.Application;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure;
using ITMartin.Magic.Infrastructure.Persistence;
using ITMartin.Magic.Infrastructure.Services;
using ITMartin.Magic.Server;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.OCR;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;

var builder =
    WebApplication.CreateBuilder(args);
builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore.Database.Command",
    LogLevel.Warning);

builder.Logging.AddFilter(
    "Microsoft.AspNetCore",
    LogLevel.Warning);
builder.Services.AddMemoryCache();
builder.Services.AddMediaCore(builder.Configuration);
builder.Services.AddMediaRuntime(builder.Configuration);
builder.Services.AddMagicApplication(builder.Configuration);
builder.Services.AddMagicInfrastructure(builder.Configuration);
builder.Services.AddAi();
builder.Services.AddOcr();
// =========================
// SERVICES
// =========================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// =========================
// SIGNALR
// =========================

builder.Services.Configure<HubOptions>(
    options =>
    {
        options.MaximumReceiveMessageSize =
            1024 * 1024 * 20;
    });


// =========================
// OPENCV
// =========================

builder.Services.AddScoped<
    IBlurDetectionService,
    OpenCvBlurDetectionService>();

builder.Services.AddScoped<
    IOcrRegionExtractor,
    OpenCvMagicCardOcrRegionExtractor>();


// =========================
// DATA FOLDERS
// =========================

var dataFolders =
    new[]
    {
        "data",
        "data/debug",
        "data/ocr"
    };

foreach (var folder in dataFolders)
{
    Directory.CreateDirectory(folder);
}

// =========================
// BUILD
// =========================

var app =
    builder.Build();

// =========================
// PIPELINE
// =========================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");

    app.UseHsts();
}
using var scope =
    app.Services.CreateScope();

var db =
    scope.ServiceProvider
        .GetRequiredService<MagicDbContext>();

await db.Database.MigrateAsync();

var importer =
    scope.ServiceProvider
        .GetRequiredService<IMagicSetImportService>();

await importer.ImportAsync(
    CancellationToken.None);

if (app.Configuration.GetValue<bool>("MagicPriser:SeedDemoData"))
    await ITMartin.Magic.Infrastructure.Persistence.DemoSeeder.SeedAsync(db);

// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAntiforgery();

// =========================
// STATIC DATA ACCESS
// =========================

var dataPath =
    Path.Combine(
        builder.Environment.ContentRootPath,
        "data");

app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider =
            new PhysicalFileProvider(dataPath),

        RequestPath =
            "/data"
    });

// =========================
// SET ICON CACHE
// =========================

// Set icons were hotlinked straight to svgs.scryfall.io on every render -
// fine on WiFi, unreliable on a phone's mobile connection mid-scan, which is
// why some icons just never showed up. There are only a few dozen distinct
// sets in play at once, so an in-memory cache (already registered below) is
// enough - no disk/volume needed, and a cold cache just refills itself on
// the next few requests after a restart.
app.MapGet("/api/set-icon/{code}", async (
    string code,
    IMagicKnowledgeService knowledgeService,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache) =>
{
    var cacheKey = $"set-icon:{code}";
    if (cache.TryGetValue(cacheKey, out byte[]? cached) && cached is not null)
    {
        return Results.File(cached, "image/svg+xml");
    }

    var sets = await knowledgeService.GetSetDefinitionsAsync();
    var iconUri = sets.FirstOrDefault(s => string.Equals(s.SetCode, code, StringComparison.OrdinalIgnoreCase))?.IconSvgUri;
    if (string.IsNullOrEmpty(iconUri))
    {
        return Results.NotFound();
    }

    try
    {
        var client = httpClientFactory.CreateClient();
        var bytes = await client.GetByteArrayAsync(iconUri);
        cache.Set(cacheKey, bytes, TimeSpan.FromDays(30));
        return Results.File(bytes, "image/svg+xml");
    }
    catch
    {
        // Scryfall unreachable right now - better to show no icon than to
        // hang the request or crash the page.
        return Results.NotFound();
    }
});

// =========================
// BOGSHOPPEN LAGER (2026-10-05)
// =========================

// Bogshoppen Lager pulls the scanned cards of one owner (e.g. "Bogshoppen") into its register with set, number
// and the Scryfall price. Server to server only, shared key Magic__LagerKey = Lager__MagicKey (magic.env).
var lagerKey = app.Configuration["Magic:LagerKey"] ?? "";
bool LagerKeyOk(HttpContext ctx) =>
    lagerKey.Length >= 16 && ctx.Request.Headers.TryGetValue("X-Lager-Key", out var k) && k == lagerKey;

app.MapGet("/api/lager/owners", async (HttpContext ctx, MagicDbContext db) =>
{
    if (!LagerKeyOk(ctx)) return Results.Unauthorized();
    var owners = await db.Cards.GroupBy(c => c.Owner)
        .Select(g => new { Owner = g.Key, Cards = g.Count(), Copies = g.Sum(c => c.Quantity), Eur = g.Sum(c => (c.EurPrice ?? 0) * c.Quantity) })
        .ToListAsync();
    return Results.Ok(owners.OrderByDescending(o => o.Copies));
});

app.MapGet("/api/lager/cards", async (HttpContext ctx, string owner, MagicDbContext db) =>
{
    if (!LagerKeyOk(ctx)) return Results.Unauthorized();
    var cards = await db.Cards.Where(c => c.Owner == owner).ToListAsync();
    var sets = await db.Sets.ToDictionaryAsync(s => s.SetCode.ToLower());
    return Results.Ok(cards.Select(c =>
    {
        var set = sets.GetValueOrDefault(c.SetCode.ToLower());
        return new
        {
            c.Id, c.Name, c.SetCode, SetName = set?.SetName ?? c.SetCode.ToUpper(), ReleaseYear = set?.ReleaseYear,
            c.CollectorNumber, c.Quantity, c.EurPrice, c.UsdPrice, c.ScryfallId,
        };
    }));
});

// =========================
// BLAZOR
// =========================

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// =========================
// RUN
// =========================

app.Run();