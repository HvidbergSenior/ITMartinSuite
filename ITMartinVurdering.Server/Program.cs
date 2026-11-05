using System.Security.Cryptography;
using System.Text;
using ITMartin.Shared.UI.Kolibri;
using ITMartinVarer.Contracts;
using ITMartinVurdering.Application;
using ITMartinVurdering.Infrastructure;

// Bogshoppen Vurdering (Kolibri Rede) - step 2 of 5, 2026-11-05 (user: "should finding prices be an app?" -> yes; "all should be
// separate responsibility"). Lager keeps the things and hunts the prices; here a person sees ONE thing at a time and says Ok
// (the suggestion) or types a price. Only Martin and Rico: Vurdering__Pin, the cookie holds a hash, never the PIN.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Bogshoppen Vurdering";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Sæt pris på tingene – én ad gangen. Tryk Ok, eller skriv din egen pris.";
    k.About =
    [
        "Vurdering er trin 2 af Bogshoppens fem trin: Tilføj varer, Find priser, Læg på lager, På bogshoppen.dk og Sælg.",
        "Lager har allerede søgt efter prisen andre steder (DBA, eBay, Tradera …). Her ser du én ting ad gangen med forslaget og det, der blev fundet – og du bestemmer.",
        "Skal vurderes = fundet til 50 kr eller mere, men uden din pris. Ingen pris = intet fundet nogen steder.",
    ];
    k.HowTo =
    [
        "Log ind med samme kode som Lager.",
        "Vælg bunken: Skal vurderes eller Ingen pris. Øverst står, hvor mange der venter.",
        "Kig på billedet og det, der blev fundet. Tryk Ok for forslaget – eller skriv din egen pris og tryk Gem.",
        "Ved du det ikke? Tryk Spring over, så kommer den næste.",
    ];
    k.Version = "2026.11.05";
    k.ThemeColor = "#0b1220";
    k.StaticPages = true;
});
builder.Services.AddVurderingApplication();
builder.Services.AddVurderingInfrastructure(builder.Configuration);

var app = builder.Build();
app.MapKolibri();
app.UseStaticFiles();

var pin = app.Configuration["Vurdering:Pin"] ?? "";
string? ownerToken = pin.Length >= 4 ? Token(pin) : null;
static string Token(string p) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("vurdering|" + p.Trim())))[..32];
bool IsOwner(HttpContext ctx) => ownerToken is not null && ctx.Request.Cookies.TryGetValue("vurdering_auth", out var v) && v == ownerToken;

var body = File.ReadAllText(Path.Combine(app.Environment.WebRootPath, "side.html"));
const string LoginBody = """
    <section class="k-card" data-nofold>
      <h1>Vurdering</h1>
      <p class="k-help">Skriv koden – den samme som til Lager.</p>
      <form method="post" action="/ind" class="vu-login">
        <label class="k-label" for="pin">Kode</label>
        <input class="k-input" id="pin" name="pin" type="password" autocomplete="current-password" required>
        <button class="k-btn k-btn-primary" type="submit">Log ind</button>
      </form>
    </section>
    """;
const string Buttons = """<button id="themeToggleBtn" class="k-btn k-btn-ghost k-btn-sm" type="button" title="Skift lys/mørk visning">Mørk</button>""";
const string Logout = """<form method="post" action="/ud" style="display:inline"><button class="k-btn k-btn-ghost k-btn-sm" type="submit">Log ud</button></form>""";

// The shared step row (KolibriSteps markup) - step 2 is ours.
static string Steps()
{
    var sb = new StringBuilder("""<nav class="k-steps" aria-label="Bogshoppen – sådan gør du"><ol>""");
    for (var i = 0; i < BogshoppenFlow.Steps.Count; i++)
    {
        var (name, url) = BogshoppenFlow.Steps[i];
        var e = System.Net.WebUtility.HtmlEncode(name);
        sb.Append(i + 1 == BogshoppenFlow.Priser
            ? $"""<li class="is-here"><span aria-current="step"><b>{i + 1}</b> {e}</span></li>"""
            : $"""<li><a href="{url}"><b>{i + 1}</b> {e}</a></li>""");
    }
    var (nextName, nextUrl) = BogshoppenFlow.Steps[BogshoppenFlow.Priser];
    return sb.Append($"""</ol><a class="k-btn k-btn-sm k-steps-next" href="{nextUrl}">Næste: {System.Net.WebUtility.HtmlEncode(nextName)} →</a></nav>""").ToString();
}

app.MapGet("/", (KolibriAppInfo info, HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-cache";
    var owner = IsOwner(ctx);
    return Results.Content($"""
        <!doctype html><html lang="da" data-family="{info.Family}"><head>
        <script>document.documentElement.setAttribute('data-theme', localStorage.getItem('vurdering_theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));</script>
        <meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
        <title>Vurdering · Bogshoppen</title><meta name="description" content="{info.Tagline}"><meta name="theme-color" content="{info.ThemeColor}">
        <meta name="robots" content="noindex">
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css?v=5"><link rel="stylesheet" href="/app.css?v=1">
        <script src="/_content/ITMartin.Shared.UI/kolibri.js" defer></script><script src="/app.js?v=1" defer></script>
        <link rel="manifest" href="/manifest.webmanifest"><link rel="apple-touch-icon" href="/{info.IconPath.TrimStart('/')}"></head>
        <body>{KolibriStaticPages.Header(info, actions: Buttons + (owner ? Logout : ""))}{(owner ? Steps() : "")}<main class="k-main"><div class="k-content">{(owner ? body : LoginBody)}</div></main>{KolibriStaticPages.Footer(info)}</body></html>
        """, "text/html; charset=utf-8");
});

app.MapPost("/ind", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    if (ownerToken is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Token(form["pin"].ToString())), Encoding.UTF8.GetBytes(ownerToken)))
    {
        await Task.Delay(800);
        return Results.Redirect("/");
    }
    ctx.Response.Cookies.Append("vurdering_auth", ownerToken, new CookieOptions { HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365) });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapPost("/ud", (HttpContext ctx) => { ctx.Response.Cookies.Delete("vurdering_auth"); return Results.Redirect("/"); }).DisableAntiforgery();

var api = app.MapGroup("/api").AddEndpointFilter(async (ctx, next) => IsOwner(ctx.HttpContext) ? await next(ctx) : Results.Unauthorized());

api.MapGet("/naeste", async (string? bunke, string? spring, ReviewUseCases review, CancellationToken ct) =>
{
    var skip = (spring ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToList();
    return await review.NextAsync(bunke, skip, ct) is { } q ? Results.Ok(q) : Results.Problem("Lager svarer ikke lige nu – prøv igen om lidt.", statusCode: 503);
});

api.MapPost("/ok", async (OkForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.OkAsync(f.Id, f.Forslag, ct)));
api.MapPost("/pris", async (PrisForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.OwnAsync(f.Id, f.Pris, ct)));
api.MapGet("/foto/{id:int}", async (int id, ReviewUseCases review, CancellationToken ct) =>
    await review.PhotoAsync(id, ct) is { } bytes ? Results.File(bytes, "image/jpeg") : Results.NotFound());

static IResult Answer(ReviewResult r) => r.Ok ? Results.Ok(new { besked = r.Message }) : Results.BadRequest(new { fejl = r.Message });

app.Run();

record OkForm(int Id, int? Forslag);
record PrisForm(int Id, string? Pris);
