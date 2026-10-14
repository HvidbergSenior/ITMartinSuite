using ITMartin.Shared.UI.Kolibri;
using ITMartinLager.Application;
using ITMartinLager.Infrastructure;
using ITMartinLager.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

// Bogshoppen Lager (2026-10-05, user: "100000+ products I need to sell ... all trade concentrated on Bogshoppen.dk").
// Step 1 of the plan: the register. ITMartin is the brand (the CVR is Den Digitale Rådgiver's); Bogshoppen is the shop name.
// Layers: Domain (rules) / Application (use cases + ports) / Infrastructure (SQLite, Claude, price sites) / this Server.
var builder = WebApplication.CreateBuilder(args);
// Every SQL statement at Information drowned real errors in the log (the jobs query every few seconds).
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddKolibri(k =>
{
    k.Name = "Bogshoppen Lager";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Tag et billede af en bunke – så står det hele på lageret.";
    k.About =
    [
        "Bogshoppen Lager er registret over alt det, Bogshoppen sælger: bøger, Anders And-blade, Jumbobøger, tegneserier, magasiner, CD'er, DVD'er, Blu-rays, konsolspil og Magic-kort.",
        "Du lægger 10-20 ting ud side om side og tager ét billede. Så læser AI'en titel, nummer, år og stand på dem alle på én gang, og de lægges i den kasse, du står ved.",
        "Hver kasse har et nummer og en QR-label, så alt kan findes igen med søgningen. Webshop, auktioner og markedspladser bygger senere på det samme register.",
    ];
    k.HowTo =
    [
        "Vælg eller opret kassen, du lægger tingene i, under Registrér.",
        "Læg en bunke ud med forsiden op, og tryk 📷 Tag billede.",
        "Ret det, AI'en har læst forkert (gul = usikker), og tryk Gem.",
        "Find alt igen under Søg – med kassenummer.",
        "Bøger: tryk 📚 Bøger og tag et billede af en bunke – så ser du pris, efterspørgsel og om bogen er værd at sælge.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
});

var dataDir = ITMartinLager.Infrastructure.DependencyInjection.DataDir(builder.Configuration);
builder.Services.AddLagerInfrastructure(builder.Configuration);
builder.Services.AddLagerApplication();
builder.Services.AddHostedService<PriceHunterJob>();
builder.Services.AddHostedService<AuctionSyncJob>();
builder.Services.AddHostedService<GoldListJob>();
builder.Services.AddHostedService<ShopJob>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("bogshoppen-lager");

builder.Services.AddRazorComponents().AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 64 * 1024);

var app = builder.Build();
app.Services.UpgradeLagerDatabase();

app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();

// One shared PIN for Martin and Rico (Lager__Pin in magic.env). Everything but login and static files needs it.
// Vareinfo calls /api/info/* with its key instead (Lager__InfoKey).
var pin = app.Configuration["Lager:Pin"] ?? "";
var infoKey = app.Configuration["Lager:InfoKey"] ?? "";
var shopKey = app.Configuration["Lager:ShopKey"] ?? "";
bool LoggedIn(HttpContext ctx) => pin.Length >= 6 && ctx.Request.Cookies["lager_auth"] == pin;
bool FromVareinfo(HttpContext ctx) => infoKey.Length >= 12 && ctx.Request.Headers["X-Info-Key"] == infoKey;
bool FromShop(HttpContext ctx) => shopKey.Length >= 12 && ctx.Request.Headers["X-Shop-Key"] == shopKey;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.EndsWith(".css") || p.EndsWith(".js")
            || p.EndsWith(".svg") || p.EndsWith(".png");
    if (p.StartsWith("/api/info/")) { if (FromVareinfo(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
    if (p.StartsWith("/api/butik/")) { if (FromShop(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
    if (open || LoggedIn(ctx)) { await next(); return; }
    if (p.StartsWith("/_blazor")) { ctx.Response.StatusCode = 401; return; }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/login", (HttpContext ctx, [FromForm(Name = "pin")] string typed) =>
{
    if (pin.Length < 6 || typed != pin) return Results.Redirect("/login?fejl=1");
    ctx.Response.Cookies.Append("lager_auth", pin, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/api/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete("lager_auth"); return Results.Redirect("/login"); });

// Photos: the item's own photo is here; pile and kolofon photos moved to Vareinfo (old links are sent on).
static IResult Jpeg(string? file) => file is null ? Results.NotFound() : Results.File(file, "image/jpeg");
app.MapGet("/varefoto/{id:int}", async (int id, ItemUseCases items) => Jpeg(await items.PhotoFileAsync(id)));
app.MapGet("/foto/{id:int}", (int id, IVareinfoGateway v) => Results.Redirect($"{v.PublicBase}/foto/{id}"));
app.MapGet("/kolofonfoto/{id:int}", (int id, IVareinfoGateway v) => Results.Redirect($"{v.PublicBase}/kolofonfoto/{id}"));

app.MapInfoApi();
app.MapShopApi();

app.MapGet("/api/eksport.csv", async (ExportUseCases export) =>
    Results.File(await export.ItemsCsvAsync(), "text/csv", $"bogshoppen-lager-{DateTime.Now:yyyy-MM-dd}.csv"));
app.MapGet("/api/boeger.csv", async (ExportUseCases export) =>
    Results.File(await export.BooksCsvAsync(), "text/csv", $"boeger-tjekket-{DateTime.Now:yyyy-MM-dd}.csv"));

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
