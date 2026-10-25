using ITMartin.Shared.UI.Kolibri;
using ITMartinLaes.Application;
using ITMartinLaes.Infrastructure;
using ITMartinLaes.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

// Kolibri Læs (2026-10-25, user: "an app that can read these [PDFs] and make them book like ... Maybe make resume of it"):
// put a PDF on the shelf, read it like a book (pages side by side, it remembers the page) and get a Danish summary - the
// whole book, the main points and each chapter. Private behind a code (the books belong to their authors). Own container
// on the photoserver, own laes.db + books/. Layers: Domain / Application / Infrastructure (SQLite, PdfPig, Claude) / Server.
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddKolibri(k =>
{
    k.Name = "Kolibri Læs";
    k.KolibriName = "Kolibri Syn";
    k.Family = "syn";
    k.Tagline = "Dine PDF-bøger som en bog – med et resumé på dansk.";
    k.About =
    [
        "Kolibri Læs gør dine PDF-bøger til bøger: siderne vises som i en bog – to ved siden af hinanden på en stor skærm, én ad gangen på telefonen – og appen husker, hvor du kom til.",
        "AI'en læser bogen og skriver et resumé på dansk: hele bogen kort, de vigtigste pointer og et par linjer om hvert kapitel. Tryk på et kapitel for at slå op i bogen.",
        "Bøgerne er dine egne og ligger på din egen server bag en kode. Resuméet laves én gang og gemmes.",
    ];
    k.HowTo =
    [
        "Tryk Vælg PDF under Læg en bog på hylden, vælg filen og tryk Læg på hylden.",
        "Tryk på bogen. Tryk 📖 Læs bogen – bladr med pilene, piletasterne eller ved at stryge til siden.",
        "Resuméet kommer af sig selv efter ca. 10 minutter (en lang bog lidt mere). Siden viser, hvor langt AI'en er.",
        "Tryk Gå til siden ved et kapitel for at læse det i bogen.",
        "Læg appen på telefonen: åbn den i Safari/Chrome, tryk Del (⬆) eller menuen ⋮ og vælg Føj til hjemmeskærm.",
    ];
    k.Version = "2026.10.25";
    k.ThemeColor = "#121917";
});

var dataDir = ITMartinLaes.Infrastructure.DependencyInjection.DataDir(builder.Configuration);
builder.Services.AddLaesInfrastructure(builder.Configuration);
builder.Services.AddLaesApplication();
builder.Services.AddHostedService<SummaryJob>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("kolibri-laes");
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = Shelf.MaxBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = Shelf.MaxBytes);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.Services.UpgradeLaesDatabase();

app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();

// One code (Laes__Pin in magic.env). The cookie holds a hash, not the code.
var pin = (app.Configuration["Laes:Pin"] ?? "").Trim();
var token = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("laes:" + pin)));
bool LoggedIn(HttpContext ctx) => pin.Length >= 6 && ctx.Request.Cookies["laes_auth"] == token;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.EndsWith(".css") || p.EndsWith(".js")
            || p.EndsWith(".svg") || p.EndsWith(".png");
    if (open || LoggedIn(ctx)) { await next(); return; }
    if (p.StartsWith("/_blazor") || p.StartsWith("/api/")) { ctx.Response.StatusCode = 401; return; }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/login", (HttpContext ctx, [FromForm(Name = "pin")] string typed) =>
{
    if (pin.Length < 6 || typed.Trim() != pin) return Results.Redirect("/login?fejl=1");
    ctx.Response.Cookies.Append("laes_auth", token, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/api/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete("laes_auth"); return Results.Redirect("/login"); });

// A PDF onto the shelf: a plain form post (a 60 MB book through the Blazor connection would be slow).
app.MapPost("/api/bog", async (HttpRequest req, Shelf shelf, CancellationToken ct) =>
{
    var form = await req.ReadFormAsync(ct);
    if (form.Files.GetFile("pdf") is not { Length: > 0 } file) return Results.Redirect("/?fejl=" + Uri.EscapeDataString("Vælg en PDF-fil først."));
    try
    {
        await using var stream = file.OpenReadStream();
        var book = await shelf.AddAsync(stream, file.FileName, ct);
        return Results.Redirect($"/bog/{book.Id}");
    }
    catch (LaesException ex) { return Results.Redirect("/?fejl=" + Uri.EscapeDataString(ex.Message)); }
}).DisableAntiforgery();

app.MapGet("/api/bog/{id:guid}/pdf", async (Guid id, Shelf shelf, CancellationToken ct) =>
    await shelf.GetAsync(id, ct) is { } book && shelf.PdfPath(book) is { } path
        ? Results.File(path, "application/pdf", enableRangeProcessing: true)
        : Results.NotFound());

// A page as a picture for the reader (made once, then kept - the browser may keep it for a month too).
app.MapGet("/api/bog/{id:guid}/side/{page:int}.jpg", async (Guid id, int page, int? b, HttpContext ctx, Shelf shelf, CancellationToken ct) =>
{
    if (await shelf.PageImageAsync(id, page, b ?? 1200, ct) is not { } jpg) return Results.NotFound();
    ctx.Response.Headers.CacheControl = "private, max-age=2592000";
    return Results.File(jpg, "image/jpeg");
});

app.MapPost("/api/bog/{id:guid}/side/{page:int}", async (Guid id, int page, Shelf shelf, CancellationToken ct) =>
{
    await shelf.TurnToAsync(id, page, ct);
    return Results.Ok();
}).DisableAntiforgery();

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
