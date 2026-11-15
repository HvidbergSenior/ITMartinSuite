using ITMartin.Shared.UI.Kolibri;
using ITMartinVareinfo.Application;
using ITMartinVareinfo.Domain;
using ITMartinVareinfo.Infrastructure;
using ITMartinVareinfo.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;

// Bogshoppen Vareinfo = the capture app (2026-10-20, user: "One app for just taking pictures and storing ... 100 people
// with own phone each"). Helpers sign in with their own code, tap a session ("Session 3 – Boxit, reol 2", 2026-10-29)
// and take photos; Martin also scans barcodes. Nothing is read here: Lager fetches the photos (/api/lager/*)
// and reads them when Martin presses Læs. Layers: Domain / Application / Infrastructure (SQLite, photo files) / this Server.
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddKolibri(k =>
{
    k.Name = "Bogshoppen Vareinfo";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Tag billeder af dine ting – så kommer de på lager.";
    k.About =
    [
        "Bogshoppen Vareinfo er kameraet til Bogshoppens egne ting. Formål: tag billeder, og læg tingene på lager.",
        "Du tager billederne med telefonens eget kamera – så mange du vil – og uploader dem her. Der er intet at vælge: dagens session hedder bare datoen, fx »15. november«, laves af sig selv og lukker ved midnat. Skriv navnet på kassen, så tingene kan findes igen.",
        "Billederne læses bagefter på Bogshoppen Lager, og tingene kommer på lager af sig selv.",
    ];
    k.HowTo =
    [
        "Log ind med din kode.",
        "Tag billederne med telefonens eget kamera: 10-20 ting med forsiden op på hvert, til alt har været på et billede.",
        "Åbn Vareinfo, tryk Upload billeder fra telefonen, og vælg billederne – mange på én gang. Skriv dagens navn på kassen.",
        "Du skal aldrig trykke Færdig eller lukke noget: billederne er gemt, så snart de er sendt, og de bliver læst bagefter.",
        "iPhone: lav genvejen »Bogshoppen foto« én gang (vejledningen står på siden under Genvej på iPhone). Så åbner ikonet telefonens eget kamera, og hvert billede sendes med det samme – billederne kommer ikke i dine egne fotos.",
        "Mister du forbindelsen, venter billederne på telefonen og sendes, når den er tilbage.",
        "Venter der billeder fra en session, der er lukket, så tryk Send dem til denne session.",
    ];
    k.Version = "2026.11.15";
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
const string ShortcutHeader = "X-Vareinfo-Kode";
var lagerKey = app.Configuration["Vareinfo:LagerKey"] ?? "";
bool FromLager(HttpContext ctx) => lagerKey.Length >= 12 && ctx.Request.Headers[CaptureHeaders.Key] == lagerKey;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    // ITMartin Admin asks with its own key - the endpoint checks it.
    if (p == ITMartinAdmin.Contracts.AdminStatusApi.Path) { await next(); return; }
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.EndsWith(".css") || p.EndsWith(".js")
            || p.EndsWith(".svg") || p.EndsWith(".png");
    if (p.StartsWith("/api/lager/")) { if (FromLager(ctx)) await next(); else ctx.Response.StatusCode = 401; return; }
    if (open) { await next(); return; }
    // The iPhone Shortcut sends the code in a header (2026-11-15) - same codes as the login, no new secret.
    var code = ctx.Request.Cookies[Cookie] ?? ctx.Request.Headers[ShortcutHeader].FirstOrDefault();
    if (await ctx.RequestServices.GetRequiredService<HelperUseCases>().SignInAsync(code) is { } me)
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

// ITMartin Admin's front page (2026-10-26): the open sessions and what is taken in them. Only with Admin__StatusKey.
app.MapGet(ITMartinAdmin.Contracts.AdminStatusApi.Path, async (HttpContext ctx, SessionUseCases sessions) =>
{
    if (!ITMartinAdmin.Contracts.AdminStatusApi.KeyOk(app.Configuration["Admin:StatusKey"], ctx.Request.Headers[ITMartinAdmin.Contracts.AdminStatusApi.KeyHeader])) return Results.Unauthorized();
    var open = (await sessions.OverviewAsync()).Where(s => s.Session.Open).ToList();
    return Results.Ok(new ITMartinAdmin.Contracts.AppStatus("Vareinfo",
    [
        new("åbne sessioner", open.Count, false, "https://vareinfo.bogshoppen.dk/"),
        new("billeder i de åbne sessioner", open.Sum(s => s.Photos), false, "https://vareinfo.bogshoppen.dk/"),
    ]));
});

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
        await capture.StoreAsync(s, int.TryParse(form["sessionId"], out var id) ? id : 0, (Taker)ctx.Items[nameof(Taker)]!);
        return Results.Ok();
    }
    // 409, not 400: the photo is fine, its session is gone - the phone keeps it and asks for a session (fotokoe.js).
    catch (SessionGoneException ex) { return Results.Conflict(ex.Message); }
    catch (VareinfoException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) when (ex is ImageFormatException or UnknownImageFormatException)   // the same photo will never open
    {
        app.Logger.LogWarning("Photo upload could not be opened: {Message}", ex.Message);
        return Results.BadRequest("Billedet kunne ikke åbnes.");
    }
}).DisableAntiforgery();

// A session photo, for Lager's item page ("billedet af bunken den blev registreret fra").
// The iPhone Shortcut "Bogshoppen foto" (2026-11-15): the phone's own camera, each photo straight here as the request body
// (or a form file) - into today's session. Answers the text the Shortcut can show: "Gemt i 15. november".
app.MapPost("/api/genvej/foto", async (HttpContext ctx, CaptureUseCases capture) =>
{
    using var ms = new MemoryStream();
    if (ctx.Request.HasFormContentType && (await ctx.Request.ReadFormAsync()).Files is { Count: > 0 } files)
        await files[0].CopyToAsync(ms);
    else
        await ctx.Request.Body.CopyToAsync(ms);
    if (ms.Length == 0) return Results.BadRequest("Intet billede.");
    try { return Results.Text("Gemt i " + await capture.StoreTodayAsync(ms.ToArray(), (Taker)ctx.Items[nameof(Taker)]!)); }
    catch (VareinfoException ex) { return Results.BadRequest(ex.Message); }
}).DisableAntiforgery();

static IResult Jpeg(string? file) => file is null ? Results.NotFound() : Results.File(file, "image/jpeg");
app.MapGet("/foto/{id:int}", async (int id, CaptureUseCases capture) => Jpeg(await capture.PhotoFileAsync(id, null)));

// A thing's picture on the Tjek page (2026-11-10): Lager's cut-out of it, else the pile photo it was read from.
app.MapGet("/api/lager/nye", async (int? antal, CaptureUseCases capture) => await capture.NewForLagerAsync(Math.Clamp(antal ?? 100, 1, 500)));
app.MapPost("/api/lager/modtaget", async (CaptureReceived body, CaptureUseCases capture) => { await capture.ReceivedAsync(body); return Results.Ok(); });
app.MapGet("/api/lager/foto/{id:int}", async (int id, int? del, CaptureUseCases capture) => Jpeg(await capture.PhotoFileAsync(id, del)));

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
