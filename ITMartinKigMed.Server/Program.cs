using ITMartin.Shared.UI.Kolibri;
using ITMartinKigMed.Server;
using ITMartinKigMed.Application;
using ITMartinKigMed.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Kig med";
    k.KolibriName = "Kolibri Reagere";
    k.Family = "reagere";
    k.Tagline = "Se Martin arbejde live – og vær med: stil spørgsmål, stem og send idéer.";
    k.About =
    [
        "Kig med viser Martin live på video, når han koder, spiller guitar eller viser en app frem.",
        "Du kan stille spørgsmål, stemme når han spørger, sende reaktioner og sende en idé eller et billede ind – direkte fra telefonen, uden app og uden konto.",
    ];
    k.HowTo =
    [
        "Åbn siden, når Martin er live – videoen starter af sig selv. Tryk 🔊 for lyd.",
        "Skriv dit navn én gang. Så kan du stille spørgsmål i feltet under videoen.",
        "Når Martin spørger om noget, dukker afstemningen op – tryk på dit svar.",
        "Tryk på en emoji for at reagere, eller send en idé/et billede med 💡.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 64 * 1024);
builder.Services.AddKigMedInfrastructure(builder.Configuration);
builder.Services.AddMediaProxy();

var app = builder.Build();
app.MapKolibri();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

// ── The studio (Martin only) is behind one PIN; watching is open to everyone.
var pin = app.Configuration["KigMed:Pin"] ?? "kigmed2026";
bool IsOwner(HttpContext ctx) => ctx.Request.Cookies.TryGetValue("kigmed_auth", out var v) && v == pin;
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (path.StartsWith("/studio", StringComparison.OrdinalIgnoreCase) && !IsOwner(ctx))
    {
        ctx.Response.Redirect("/login");
        return;
    }
    await next();
});

app.MapPost("/api/auth/login", (HttpContext ctx, [FromForm] string pin_) =>
{
    if (pin_ != pin) return Results.Redirect("/login?error=1");
    ctx.Response.Cookies.Append("kigmed_auth", pin, new CookieOptions
    {
        HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365),
        Secure = ctx.Request.IsHttps || ctx.Request.Headers.ContainsKey("CF-Connecting-IP"),
    });
    return Results.Redirect("/studio");
}).DisableAntiforgery();

app.MapGet("/api/auth/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("kigmed_auth");
    return Results.Redirect("/");
});

// Live or not, for itmartin.dk's front page (hjem-web asks this over the LAN every 30 s).
app.MapGet("/api/status", (LiveHub hub, LiveVideo video, HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    var (title, next) = hub.Read(s => (s.Title, s.Next));
    // "live" only while video is actually arriving - not just because "Gå live" was pressed once
    return Results.Ok(new { live = video.IsLive, title, next, viewers = hub.Viewers });
});

// Pictures viewers sent in. A picture Martin has not shown is only for the studio.
app.MapGet("/pic/{name}", (string name, HttpContext ctx, LiveHub hub) =>
    hub.PictureFile(name, IsOwner(ctx)) is { } file ? Results.File(file, "image/jpeg") : Results.NotFound());

app.MapMediaProxy(IsOwner);

app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
