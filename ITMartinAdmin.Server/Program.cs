using ITMartin.Shared.UI.Kolibri;
using ITMartinAdmin.Application;
using ITMartinAdmin.Infrastructure;
using ITMartinAdmin.Server;
using Microsoft.AspNetCore.Mvc;

// ITMartinAdmin (2026-10-26, user: "i need a ITMartinAdmin" -> "one front door"): one login (Admin__Pin) and one page
// with buttons to every admin page and the apps' live numbers (each app's GET /api/admin/status, Admin__StatusKey).
// Layers: Domain (the links, the rules for the numbers) / Application (the front page use case + port) /
// Infrastructure (asking the apps) / this Server. Photoserver :9157.
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

builder.Services.AddKolibri(k =>
{
    k.Name = "ITMartin Admin";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Alle dine admin-sider ét sted – og det, der venter på dig.";
    k.About =
    [
        "ITMartin Admin er indgangen til alle admin-siderne: Bogshoppen (Sessioner, Kasser, Ordrer, Interesse), ITMartin (Svar, Kunder, Stem) og Drift (Kontrol).",
        "Øverst står det, der venter på dig – fx ordrer, der skal pakkes. Tallene kommer fra apps'ene, lige når du åbner siden.",
    ];
    k.HowTo =
    [
        "Log ind med din PIN-kode.",
        "Se øverst, om noget venter på dig. Tryk på knappen ved det, så kommer du direkte til siden.",
        "Under Bogshoppen, ITMartin og Drift er knapperne til alle admin-siderne. Hver side beder stadig om sin egen kode første gang.",
        "Svarer en app ikke, står der det – den er måske stoppet i Kontrol.",
        "Læg siden på telefonen: iPhone – Safari, Del, Føj til hjemmeskærm. Android – Chrome, de tre prikker, Føj til startskærm.",
    ];
    k.Version = "2026.10.26";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddAdminInfrastructure(builder.Configuration);
builder.Services.AddAdminApplication();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();

// Martin's PIN (Admin__Pin in magic.env) - a PIN shorter than 6 never opens anything.
var pin = app.Configuration["Admin:Pin"] ?? "";
bool LoggedIn(HttpContext ctx) => pin.Length >= 6 && ctx.Request.Cookies[AdminCookie.Name] == pin;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.EndsWith(".css") || p.EndsWith(".js")
            || p.EndsWith(".svg") || p.EndsWith(".png");
    if (open || LoggedIn(ctx)) { await next(); return; }
    if (p.StartsWith("/_blazor")) { ctx.Response.StatusCode = 401; return; }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/login", (HttpContext ctx, [FromForm(Name = "pin")] string typed) =>
{
    if (pin.Length < 6 || typed != pin) return Results.Redirect("/login?fejl=1");
    ctx.Response.Cookies.Append(AdminCookie.Name, pin, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/api/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete(AdminCookie.Name); return Results.Redirect("/login"); });

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

namespace ITMartinAdmin.Server
{
    public static class AdminCookie
    {
        public const string Name = "itmartin_admin";
    }
}

public partial class Program;
