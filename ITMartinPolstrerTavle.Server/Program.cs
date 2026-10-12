using ITMartinPolstrerTavle.Server;
using ITMartin.Shared.UI.Kolibri;
using ITMartinPolstrer.Application;
using ITMartinPolstrer.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 200 * 1024 * 1024);

// Polstrer-tavlen = the paid version of Polstrer for a company of polstrere. Same Domain/Application/Infrastructure as the
// phone app (one database, one media folder); this Server is only the board pages (DDD/Clean since 2026-10-12).
builder.Services.AddPolstrerApplication();
builder.Services.AddPolstrerInfrastructure(builder.Configuration);

// Kolibri.UI identity: /om, /hjaelp, /health, PWA manifest, footer - the same templates as every app in the suite.
builder.Services.AddKolibri(k =>
{
    k.Name = "Værkstedstavle";
    k.KolibriName = "Kolibri Hukommelse";
    k.Family = "hukommelse";
    k.Tagline = "Hele værkstedet på én skærm.";
    k.About =
    [
        "Tavlen viser alle møbler i værkstedet: hvad der venter, hvad der er i gang, og hvad der er færdigt eller afleveret.",
        "Den hører til Polstrer-appen: billeder og noter fra telefonen dukker op her af sig selv.",
        "Alle i værkstedet kan skrive en kommentar på et møbel – fx at fjedrene er bestilt, eller at kunden har ringet.",
    ];
    k.HowTo =
    [
        "Sæt tavlen på et tv eller en stor skærm i værkstedet – den opdaterer sig selv hvert 30. sekund.",
        "Tryk på et møbel for at rette det, skifte status eller skrive en kommentar.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#121917";
});

// Keys on the data volume, otherwise every deploy invalidates every
// antiforgery cookie already sitting in her browser.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Polstrer:KeysDir"] ?? "/app/data/keys-tavle"))
    .SetApplicationName("polstrer-tavle");

var app = builder.Build();
app.MapKolibri();

await app.Services.UpgradePolstrerDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// Photos/videos served straight from the data volume.
var mediaRoot = app.Services.MediaRoot();
Directory.CreateDirectory(mediaRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRoot),
    RequestPath = "/media"
});

// Single-PIN login for her; /vis/{slug} (the share page for customers and
// colleagues) and the media behind it are open, everything else is gated.
var adminPin = app.Configuration["Polstrer:AdminPin"] ?? "polstrer2026";

app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var open = path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/om", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/hjaelp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/hjaelp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/manifest.webmanifest", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/kolibri-sw.js", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/kolibri/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/vis/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
    if (open || (ctx.Request.Cookies.TryGetValue("polstrer_auth", out var v) && v == adminPin))
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
        ctx.Response.Cookies.Append("polstrer_auth", adminPin, new CookieOptions
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
    ctx.Response.Cookies.Delete("polstrer_auth");
    return Results.Redirect("/login");
});

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
