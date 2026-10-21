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
        "Billederne tages i Vareinfo: hver hjælper har sin egen kasse og tager billeder af 10-20 ting ad gangen. Når du trykker Læs, læser AI'en titel, nummer, år og stand på dem alle, og de lægges i kassen her.",
        "Hver kasse har et nummer og en QR-label, så alt kan findes igen med søgningen. Webshop, auktioner og markedspladser bygger senere på det samme register.",
    ];
    k.HowTo =
    [
        "Hjælperne tager billeder af kasserne i Vareinfo (📷 Registrér) – hver med sin egen kode og sit eget kassenummer.",
        "Nye billeder kommer herind af sig selv hvert minut. Under 📦 Kasser trykker du Læs ved en kasse – så læser AI'en billederne og lægger tingene i kassen.",
        "Ret det, AI'en har læst forkert, på varens side.",
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
builder.Services.AddHostedService<IntakeJob>();
builder.Services.AddHostedService<KolofonJob>();
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

// One shared PIN for Martin and Rico (Lager__Pin in magic.env). Everything but login and static files needs it; Salg
// calls /api/salg/* with its key instead.
var pin = app.Configuration["Lager:Pin"] ?? "";
var salgKey = app.Configuration["Lager:SalgKey"] ?? "";
bool LoggedIn(HttpContext ctx) => pin.Length >= 6 && ctx.Request.Cookies["lager_auth"] == pin;
bool FromSalg(HttpContext ctx) => salgKey.Length >= 12 && ctx.Request.Headers[StockHeaders.Key] == salgKey;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.EndsWith(".css") || p.EndsWith(".js")
            || p.EndsWith(".svg") || p.EndsWith(".png");
    if (p.StartsWith("/api/salg/")) { if (FromSalg(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
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

// Photos: the item's own photo and its kolofon are here; the box photos are in Vareinfo (old links are sent on).
static IResult Jpeg(string? file) => file is null ? Results.NotFound() : Results.File(file, "image/jpeg");
app.MapGet("/varefoto/{id:int}", async (int id, ItemUseCases items) => Jpeg(await items.PhotoFileAsync(id)));
app.MapGet("/foto/{id:int}", (int id, VareinfoLinks v) => Results.Redirect(v.Photo(id)));
app.MapGet("/kolofonfoto/{id:int}", async (int id, KolofonUseCases k) => Jpeg(await k.PhotoFileAsync(id)));

app.MapStockApi();

app.MapGet("/api/eksport.csv", async (ExportUseCases export) =>
    Results.File(await export.ItemsCsvAsync(), "text/csv", $"bogshoppen-lager-{DateTime.Now:yyyy-MM-dd}.csv"));
// Item photos as a zip: all, or one box (?kasse=K0001). Built in a temp file that deletes itself when sent - all photos
// can be more than should sit in memory.
app.MapGet("/api/varefotos.zip", async (string? kasse, ExportUseCases export, CancellationToken ct) =>
{
    var file = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);
    if (await export.PhotosZipAsync(file, kasse, ct) == 0)
    {
        await file.DisposeAsync();
        return Results.Text("Der er ingen varefotos" + (kasse is null ? "" : $" i {kasse}") +
            " endnu. Åbn en vare og tryk 'Tag billede af varen' – så kommer billedet med her.", "text/plain; charset=utf-8");
    }
    file.Position = 0;
    return Results.File(file, "application/zip", $"varefotos-{kasse ?? "alle"}-{DateTime.Now:yyyy-MM-dd}.zip");
});
app.MapGet("/api/boeger.csv", async (ExportUseCases export) =>
    Results.File(await export.BooksCsvAsync(), "text/csv", $"boeger-tjekket-{DateTime.Now:yyyy-MM-dd}.csv"));

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
