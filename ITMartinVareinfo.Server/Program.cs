using ITMartin.Shared.UI.Kolibri;
using ITMartinVareinfo.Application;
using ITMartinVareinfo.Infrastructure;
using ITMartinVareinfo.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

// Bogshoppen Vareinfo (2026-10-12): split out of Lager - user: "one is for pricing and storage. No matter what it is.
// Books, dvds or whatever. and another app is for search and adding info on each product". This app finds out WHAT a
// thing is (photo of a pile, barcodes, kolofons, collector texts) and hands it to Lager, which keeps the boxes and prices.
// Layers: Domain / Application (use cases + ports) / Infrastructure (SQLite, Claude, barcode sites, Lager's API) / this Server.
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddKolibri(k =>
{
    k.Name = "Bogshoppen Vareinfo";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Tag et billede af en bunke – så ved du hvad det hele er.";
    k.About =
    [
        "Bogshoppen Vareinfo finder ud af hvad tingene er: bøger, Anders And-blade, Jumbobøger, tegneserier, magasiner, CD'er, DVD'er, Blu-rays, konsolspil, Magic-kort og merchandise.",
        "Du lægger 10-20 ting ud side om side og tager ét billede. Så læser AI'en titel, nummer, år og stand på dem alle på én gang. Stregkoder slås op på nettet, og kolofoner (udgave og oplag) læses af sig selv.",
        "Det hele lægges i en kasse på Bogshoppen Lager, hvor prisjægeren finder priserne, og hvor alt kan findes igen.",
    ];
    k.HowTo =
    [
        "Vælg eller opret kassen, du lægger tingene i.",
        "Læg en bunke ud med forsiden op, og tryk 📷 Tag billede – eller scan stregkoderne.",
        "Ret det, AI'en har læst forkert (⚠ = usikker), og tryk Gem. Så står det på Lager.",
        "Kolofon: bøger, hvor udgaven kan betyde noget for prisen, står under 📖 Kolofon. Tag et billede af kolofonen.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
});

var dataDir = ITMartinVareinfo.Infrastructure.DependencyInjection.DataDir(builder.Configuration);
builder.Services.AddVareinfoInfrastructure(builder.Configuration);
builder.Services.AddVareinfoApplication();
builder.Services.AddHostedService<KolofonJob>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("bogshoppen-vareinfo");

builder.Services.AddRazorComponents().AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 64 * 1024);

var app = builder.Build();
var imported = app.Services.UpgradeVareinfoDatabase(app.Configuration);
if (imported > 0) app.Logger.LogInformation("Took over {Count} photos from Lager", imported);

app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();

// One shared PIN for Martin and Rico (Vareinfo__Pin). Lager calls /api/lager/* with its key instead.
var pin = app.Configuration["Vareinfo:Pin"] ?? "";
var lagerKey = app.Configuration["Vareinfo:LagerKey"] ?? "";
bool LoggedIn(HttpContext ctx) => pin.Length >= 6 && ctx.Request.Cookies["vareinfo_auth"] == pin;
bool FromLager(HttpContext ctx) => lagerKey.Length >= 12 && ctx.Request.Headers["X-Info-Key"] == lagerKey;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.EndsWith(".css") || p.EndsWith(".js")
            || p.EndsWith(".svg") || p.EndsWith(".png");
    if (p.StartsWith("/api/lager/")) { if (FromLager(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
    if (open || LoggedIn(ctx)) { await next(); return; }
    if (p.StartsWith("/_blazor")) { ctx.Response.StatusCode = 401; return; }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/login", (HttpContext ctx, [FromForm(Name = "pin")] string typed) =>
{
    if (pin.Length < 6 || typed != pin) return Results.Redirect("/login?fejl=1");
    ctx.Response.Cookies.Append("vareinfo_auth", pin, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/api/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete("vareinfo_auth"); return Results.Redirect("/login"); });

// Photos: the pile photo (to check a wrong reading against) and a book's kolofon. Lager's item page links here.
static IResult Jpeg(string? file) => file is null ? Results.NotFound() : Results.File(file, "image/jpeg");
app.MapGet("/foto/{id:int}", async (int id, ReadingUseCases reading) => Jpeg(await reading.PhotoFileAsync(id)));
app.MapGet("/kolofonfoto/{itemId:int}", async (int itemId, KolofonUseCases k) => Jpeg(await k.PhotoFileAsync(itemId)));

// For Lager's Bøger page (pricing a pile): read a photo, look up a barcode. Same use cases as the pages here.
app.MapPost("/api/lager/laes", async (HttpRequest req, ReadingUseCases reading) =>
{
    var form = await req.ReadFormAsync();
    if (form.Files.Count == 0) return Results.BadRequest();
    await using var s = form.Files[0].OpenReadStream();
    try
    {
        var (photoId, found) = await reading.ReadPileAsync(s, form["hint"].ToString(), null, form["label"].ToString());
        return Results.Ok(new { PhotoId = photoId, Found = found, CallsToday = await reading.CallsTodayAsync() });
    }
    catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 503); }   // the daily cap etc.
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        app.Logger.LogWarning("Pile photo from Lager could not be read: {Message}", ex.Message);
        return Results.Problem("Billedet kunne ikke åbnes. Tag et nyt billede.", statusCode: 400);
    }
}).DisableAntiforgery();
app.MapGet("/api/lager/stregkode/{code}", async (string code, ReadingUseCases reading) =>
    await reading.LookupBarcodeAsync(code) is { } f ? Results.Ok(f) : Results.NotFound());

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
