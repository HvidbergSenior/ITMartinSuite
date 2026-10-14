using System.Security.Cryptography;
using System.Text;
using ITMartin.Shared.UI.Kolibri;
using ITMartinSkrivSange.Application;
using ITMartinSkrivSange.Infrastructure;
using ITMartinSkrivSange.Server;
using Microsoft.AspNetCore.Mvc;

// SkrivSange - write your own songs (split out of Musik Studio 2026-09-15; DDD/Clean 2026-10-14).
// Layers: Domain (songs, chords, capo, suggestions) / Application (use cases + ports) / Infrastructure (skrivsange.db,
// the studio's studio.db, the music folder, ffmpeg/Demucs/librosa) / this Server.
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

builder.Services.AddKolibri(k =>
{
    k.Name = "SkrivSange";
    k.KolibriName = "Kolibri Tunge";
    k.Family = "tunge";
    k.Tagline = "Skriv din egen sang - del for del, med greb du kan spille.";
    k.About =
    [
        "SkrivSange hjælper dig med at skrive en sang på guitar: Intro, Vers 1, Vers 2, Bridge, Vers 3 og Outro.",
        "Hver del får en akkordrække i åbne greb (C, G, D, Am, Em ...). Capoen flytter tonearten, så grebene er de samme.",
        "Du kan høre akkorderne spillet med et enkelt anslag, og du skriver tekstnoter til hver del undervejs.",
    ];
    k.HowTo =
    [
        "Tryk Ny sang og giv den en titel.",
        "Vælg stemning, og tryk Foreslå på Vers 1 - eller vælg selv en akkordrække.",
        "Tryk Foreslå på de andre dele: versene bliver ens, broen bliver anderledes, intro og outro vokser ud af verset.",
        "Tryk Spil for at høre en del. Skriv tekstnoter i feltet under akkorderne - det gemmes af sig selv.",
    ];
    k.Version = "2026.10";
});

builder.Services.AddSkrivSangeInfrastructure(builder.Configuration);
builder.Services.AddSkrivSangeApplication();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 1024 * 1024);

var app = builder.Build();
app.Services.UpgradeSkrivSangeDatabases();

app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();

// The studio's code (StudioSettings__AccessCode in magic.env). Checked on the server - the cookie holds a hash, not the code.
var code = (app.Configuration["StudioSettings:AccessCode"] ?? "").Trim();
var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("skrivsange:" + code.ToLowerInvariant())));
bool LoggedIn(HttpContext ctx) => code.Length > 0 && ctx.Request.Cookies["skrivsange_auth"] == token;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    var open = p == "/" || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.StartsWith("/Error")
            || p.EndsWith(".css") || p.EndsWith(".js") || p.EndsWith(".svg") || p.EndsWith(".png");
    if (p == "/" && LoggedIn(ctx)) { ctx.Response.Redirect("/sange"); return; }
    if (open || LoggedIn(ctx)) { await next(); return; }
    if (p.StartsWith("/_blazor") || p.StartsWith("/api/") || p.StartsWith("/stream") || p.StartsWith("/download"))
    {
        ctx.Response.StatusCode = 401;
        return;
    }
    ctx.Response.Redirect("/");
});

app.MapPost("/api/login", (HttpContext ctx, [FromForm(Name = "kode")] string typed) =>
{
    if (code.Length == 0 || !string.Equals(typed.Trim(), code, StringComparison.OrdinalIgnoreCase)) return Results.Redirect("/?fejl=1");
    ctx.Response.Cookies.Append("skrivsange_auth", token, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/sange");
}).DisableAntiforgery();

app.MapGet("/api/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete("skrivsange_auth"); return Results.Redirect("/"); });

app.MapMusicFiles();

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
