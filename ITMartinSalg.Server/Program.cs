using ITMartin.Shared.UI.Kolibri;
using ITMartinSalg.Application;
using ITMartinSalg.Infrastructure;
using ITMartinSalg.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

// Bogshoppen Salg (2026-10-21, split out of Lager - user: "i want one app one responsible ... like micro services and
// ddd/clean"): the shop API bogshoppen.dk calls, Køb nu through Stripe, the orders and the day offers. Lager keeps the
// stock; Salg asks it what is for sale and tells it what was sold. Own container, own salg.db, started and stopped in
// Kontrol. Layers: Domain (rules) / Application (use cases + ports) / Infrastructure (SQLite, Stripe, Lager) / this Server.
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddKolibri(k =>
{
    k.Name = "Bogshoppen Salg";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Det, der er købt på bogshoppen.dk – pak det og send det.";
    k.About =
    [
        "Bogshoppen Salg er salget fra butikken på bogshoppen.dk: Køb nu, kurven, betalingen med Stripe og dagens tilbud.",
        "Når noget er betalt, får Bogshoppen Lager besked og trækker det fra lageret. Selve tingene, kasserne og priserne er i Lager.",
    ];
    k.HowTo =
    [
        "🧾 Ordrer: pak det, der er betalt, og tryk 📦 Pakket. Skriv pakkens nummer og tryk Sendt – eller tryk Afhentet. Trykker du forkert, så tryk Fortryd.",
        "🏷️ Tilbud: slå dagens tilbud til og fra, ret tallene, eller lav et nyt.",
        "⭐ Interesse: de ting, kunderne har markeret som interessante på bogshoppen.dk – flest markeringer øverst. Brug listen, når I vælger ting til en liveauktion.",
        "📦 Lager åbner lageret, hvor tingene og priserne er.",
    ];
    k.Version = "2026.10.26";
    k.ThemeColor = "#f4f6f5";
});

var dataDir = ITMartinSalg.Infrastructure.DependencyInjection.DataDir(builder.Configuration);
builder.Services.AddSalgInfrastructure(builder.Configuration);
builder.Services.AddSalgApplication();
builder.Services.AddHostedService<OrderJob>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("bogshoppen-salg");

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.Services.UpgradeSalgDatabase(builder.Configuration);

app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();

// One shared PIN for Martin and Rico (Salg__Pin in magic.env). bogshoppen.dk calls /api/butik/* with its key instead.
var pin = app.Configuration["Salg:Pin"] ?? "";
var shopKey = app.Configuration["Salg:ShopKey"] ?? "";
bool LoggedIn(HttpContext ctx) => pin.Length >= 6 && ctx.Request.Cookies["salg_auth"] == pin;
bool FromShop(HttpContext ctx) => shopKey.Length >= 12 && ctx.Request.Headers[ShopHeaders.Key] == shopKey;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.EndsWith(".css") || p.EndsWith(".js")
            || p.EndsWith(".svg") || p.EndsWith(".png");
    if (p.StartsWith("/api/butik/")) { if (FromShop(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
    if (open || LoggedIn(ctx)) { await next(); return; }
    if (p.StartsWith("/_blazor")) { ctx.Response.StatusCode = 401; return; }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/login", (HttpContext ctx, [FromForm(Name = "pin")] string typed) =>
{
    if (pin.Length < 6 || typed != pin) return Results.Redirect("/login?fejl=1");
    ctx.Response.Cookies.Append("salg_auth", pin, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/api/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete("salg_auth"); return Results.Redirect("/login"); });

app.MapShopApi();

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
