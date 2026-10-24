using ITMartin.Shared.UI.Kolibri;
using ITMartinVareinfo.Application;
using ITMartinVareinfo.Domain;
using ITMartinVareinfo.Infrastructure;
using ITMartinVareinfo.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;

// Bogshoppen Vareinfo = the capture app (2026-10-20, user: "One app for just taking pictures and storing ... select the
// relevant box number ... 100 people on 100 boxes with own phone each"). Helpers sign in with their own code, type the
// number on their box and take photos and scan barcodes. Nothing is read here: Lager fetches the photos (/api/lager/*)
// and reads them when Martin presses Læs. Layers: Domain / Application / Infrastructure (SQLite, photo files) / this Server.
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddKolibri(k =>
{
    k.Name = "Bogshoppen Vareinfo";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Skriv kassens nummer – og tag billeder af alt i den.";
    k.About =
    [
        "Bogshoppen Vareinfo er kameraet til Bogshoppens kasser: bøger, Anders And-blade, tegneserier, blade, CD'er, plader, film, spil og Magic-kort.",
        "Hver hjælper har sin egen kode og sin egen kasse. Mange kan tage billeder på samme tid – hver med sin egen telefon og sin egen kasse.",
        "Billederne gemmes bare. Bagefter læses de på Bogshoppen Lager, hvor tingene lægges i kassen og får en pris.",
    ];
    k.HowTo =
    [
        "Log ind med din egen kode (du får den af Martin) – eller med dit kundenummer, hvis Martin har gjort dig til hjælper i Kunder.",
        "Skriv nummeret, der står på kassen, og tryk Start kassen. Bruger en anden den kasse, får du besked – tag så en anden.",
        "Læg 10-20 ting fra kassen ud med forsiden op, og tryk 📸 Tag billede. Gentag til kassen er tom. Stregkoder kan også scannes – bip, næste.",
        "Tryk ✓ Kassen er færdig – så kan du tage den næste kasse.",
        "Mister du forbindelsen, venter billederne på telefonen og sendes, når den er tilbage.",
    ];
    k.Version = "2026.10.24";
    k.ThemeColor = "#f4f6f5";
});

var dataDir = ITMartinVareinfo.Infrastructure.DependencyInjection.DataDir(builder.Configuration);
builder.Services.AddVareinfoInfrastructure(builder.Configuration);
builder.Services.AddVareinfoApplication();
builder.Services.AddHostedService<PhotoJob>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("bogshoppen-vareinfo");

builder.Services.AddRazorComponents().AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 64 * 1024);

var app = builder.Build();
app.Services.UpgradeVareinfoDatabase();

app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();

// Each helper signs in with their own code (Martin's = Vareinfo__Pin); the cookie holds the code, and who it is goes in
// HttpContext.Items for the pages and the upload. Lager calls /api/lager/* with its key instead.
const string Cookie = "vareinfo_auth";
var lagerKey = app.Configuration["Vareinfo:LagerKey"] ?? "";
bool FromLager(HttpContext ctx) => lagerKey.Length >= 12 && ctx.Request.Headers[CaptureHeaders.Key] == lagerKey;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.EndsWith(".css") || p.EndsWith(".js")
            || p.EndsWith(".svg") || p.EndsWith(".png");
    if (p.StartsWith("/api/lager/")) { if (FromLager(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
    if (open) { await next(); return; }
    if (await ctx.RequestServices.GetRequiredService<HelperUseCases>().SignInAsync(ctx.Request.Cookies[Cookie]) is { } me)
    {
        ctx.Items[nameof(Taker)] = me;
        await next();
        return;
    }
    if (p.StartsWith("/_blazor") || p.StartsWith("/api/")) { ctx.Response.StatusCode = 401; return; }   // the photo queue keeps the photo and asks for login
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/login", async (HttpContext ctx, [FromForm(Name = "kode")] string typed, HelperUseCases helpers) =>
{
    if (await helpers.SignInAsync(typed) is null) return Results.Redirect("/login?fejl=1");
    ctx.Response.Cookies.Append(Cookie, typed.Trim(), new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/api/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete(Cookie); return Results.Redirect("/login"); });

// The pages' photos (fotokoe.js): a plain upload instead of the Blazor connection, which phones drop while the camera
// app is open (photos lost without a word, 2026-10-19). The phone keeps the photo until this says OK. Only kept here -
// the reading copy is made by PhotoJob - so the answer comes at once, also with 100 phones sending.
app.MapPost("/api/foto", async (HttpContext ctx, CaptureUseCases capture) =>
{
    var form = await ctx.Request.ReadFormAsync();
    if (form.Files.Count == 0) return Results.BadRequest("Intet billede.");
    await using var s = form.Files[0].OpenReadStream();
    try
    {
        await capture.StoreAsync(s, form["boxCode"].ToString(), (Taker)ctx.Items[nameof(Taker)]!);
        return Results.Ok();
    }
    catch (VareinfoException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) when (ex is ImageFormatException or UnknownImageFormatException)   // the same photo will never open
    {
        app.Logger.LogWarning("Photo upload could not be opened: {Message}", ex.Message);
        return Results.BadRequest("Billedet kunne ikke åbnes.");
    }
}).DisableAntiforgery();

// A box photo, for Lager's item page ("billedet af bunken den blev registreret fra").
static IResult Jpeg(string? file) => file is null ? Results.NotFound() : Results.File(file, "image/jpeg");
app.MapGet("/foto/{id:int}", async (int id, CaptureUseCases capture) => Jpeg(await capture.PhotoFileAsync(id, null)));

// Lager's side (CaptureHeaders.Key): what is new, what it has taken over, and a photo or close-up when it reads it.
app.MapGet("/api/lager/nye", async (int? antal, CaptureUseCases capture) => await capture.NewForLagerAsync(Math.Clamp(antal ?? 100, 1, 500)));
app.MapPost("/api/lager/modtaget", async (CaptureReceived body, CaptureUseCases capture) => { await capture.ReceivedAsync(body); return Results.Ok(); });
app.MapGet("/api/lager/foto/{id:int}", async (int id, int? del, CaptureUseCases capture) => Jpeg(await capture.PhotoFileAsync(id, del)));

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
