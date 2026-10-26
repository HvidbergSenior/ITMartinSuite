using ITMartin.Shared.UI.Kolibri;
using ITMartinKunder.Infrastructure;
using ITMartinKunder.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

// Kunder (2026-10-21, user: "kundekartotek should be for all apps"): ONE Kundekartotek for itmartin.dk, bogshoppen.dk,
// Stem, Salg ... (not Bibliotek). One yes to ITMartin = all the free apps Martin has switched on here. Own container on the
// photoserver, own kunder.db, always on in Kontrol (a stopped Stem lost every sign-up on itmartin.dk 19-21 Oct).
// Layers: Domain (rules) / Application (use cases + ports) / Infrastructure (SQLite) / this Server.
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

builder.Services.AddKolibri(k =>
{
    k.Name = "Kunder";
    k.KolibriName = "Kolibri Hukommelse";
    k.Family = "hukommelse";
    k.Tagline = "Alle kunder fra alle apps – ét sted.";
    k.About =
    [
        "Kunder er kundekartoteket for alle apps: dem, der har sagt ja tak til at blive skrevet op, og dem der har købt på bogshoppen.dk.",
        "Ét ja til ITMartin giver adgang til alle de gratis apps, der er slået til under Gratis apps. Ingen reklamer og ingen nyhedsbreve.",
    ];
    k.HowTo =
    [
        "Kunder: søg på navn eller e-mail. Tryk på en kunde for at se hvad de har sagt ja til, og hvad de har købt. Afmeld eller slet herfra.",
        "Gratis apps: slå en app til eller fra. Kun de apps, der er slået til, kan prøves gratis og står ved tilmeldingen på itmartin.dk.",
        "Hent som CSV giver dem, der stadig har sagt ja – til Bcc-linjen. Skriv aldrig til flere i Til eller Cc.",
        "Hjælpere: tryk \"Gør til hjælper\" på en kunde. Så logger de ind på Vareinfo og Lager med deres kundenummer (Lager: kun kasser og varer, ingen priser). \"Fjern som hjælper\" lukker igen inden for 10 minutter.",
        "Hent data: på en kunde giver \"Hent data\" alt, vi har om personen (som fil) – send den, når nogen beder om deres data.",
        "Afmeldte slettes af sig selv efter 30 dage; køb gemmes i 5 år (bogføringsloven). Slet sletter med det samme.",
        "Skriv: send mailen \"Vi har fået din tilmelding\" til alle, der ikke har fået den – én mail pr. kunde med deres eget kundenummer og afmeld-link. Tryk Send og så Ja; ingen får den to gange.",
    ];
    k.Version = "2026.10.24";
    k.ThemeColor = "#f4f6f5";
});

var dataDir = ITMartinKunder.Infrastructure.DependencyInjection.DataDir(builder.Configuration);
builder.Services.AddKunderInfrastructure(builder.Configuration);
builder.Services.AddKunderApplication();
builder.Services.AddHostedService<CleanUpJob>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("kunder");

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.Services.UpgradeKunderDatabase();

app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();

// Martin's PIN (Kunder__Pin) for the pages; the apps call /api/kunder/* with Kunder__ApiKey (the same key in every app).
var pin = app.Configuration["Kunder:Pin"] ?? "";
var apiKey = app.Configuration["Kunder:ApiKey"] ?? "";
bool LoggedIn(HttpContext ctx) => pin.Length >= 6 && ctx.Request.Cookies["kunder_auth"] == pin;
bool FromApp(HttpContext ctx) => apiKey.Length >= 16 && ctx.Request.Headers[KunderApi.KeyHeader] == apiKey;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    // ITMartin Admin asks with its own key - the endpoint checks it.
    if (p == ITMartinAdmin.Contracts.AdminStatusApi.Path) { await next(); return; }
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/afmeld") || p == KunderApi.FreeApps
            || p.StartsWith("/_framework") || p.StartsWith("/_content") || p.StartsWith("/kolibri") || p.StartsWith("/om")
            || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp") || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest")
            || p.EndsWith(".css") || p.EndsWith(".js") || p.EndsWith(".svg") || p.EndsWith(".png");
    if (p.StartsWith("/api/kunder/")) { if (FromApp(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
    if (open || LoggedIn(ctx)) { await next(); return; }
    if (p.StartsWith("/_blazor")) { ctx.Response.StatusCode = 401; return; }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/login", (HttpContext ctx, [FromForm(Name = "pin")] string typed) =>
{
    if (pin.Length < 6 || typed != pin) return Results.Redirect("/login?fejl=1");
    ctx.Response.Cookies.Append("kunder_auth", pin, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

// ITMartin Admin's front page (2026-10-26): the customer list in numbers. Only with Admin__StatusKey.
app.MapGet(ITMartinAdmin.Contracts.AdminStatusApi.Path, async (HttpContext ctx, Kundekartotek k) =>
{
    if (!ITMartinAdmin.Contracts.AdminStatusApi.KeyOk(app.Configuration["Admin:StatusKey"], ctx.Request.Headers[ITMartinAdmin.Contracts.AdminStatusApi.KeyHeader])) return Results.Unauthorized();
    var all = await k.ListAsync();
    var week = DateTime.UtcNow.AddDays(-7);
    return Results.Ok(new ITMartinAdmin.Contracts.AppStatus("Kunder",
    [
        new("nye kunder de sidste 7 dage", all.Count(c => c.Active && c.CreatedAt >= week), false, "https://kunder.itmartin.dk/"),
        new("kunder i alt", all.Count(c => c.Active)),
        new("hjælpere", all.Count(c => c.HelperWorks)),
    ]));
});

app.MapGet("/api/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete("kunder_auth"); return Results.Redirect("/login"); });

app.MapKunderApi();

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
