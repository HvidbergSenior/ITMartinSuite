using System.Security.Cryptography;
using System.Text;
using ITMartin.Shared.UI.Kolibri;
using ITMartinVarer.Contracts;
using ITMartinVurdering.Application;
using ITMartinVurdering.Domain;
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
        "Lager har allerede søgt efter prisen andre steder (DBA, Faraos, Nemos …). Her ser du én ting ad gangen med forslaget og det, der blev fundet – og du bestemmer.",
        "Bøger prissættes kun efter danske sider – og med prisen for en ny hos Bog & idé ved siden af (2026-11-06).",
        "Skal vurderes = fundet til 50 kr eller mere, men uden din pris. Ingen pris = intet fundet nogen steder.",
        "Dødsbo og loft: tag et billede af hver ting fra et dødsbo, et loft eller en kælder. AI'en fortæller, hvad det er og historien bag det, hvad det cirka er værd brugt i Danmark, og om du skal sælge det, sælge det samlet, give det væk, smide det ud eller spørge en ekspert – og hvor det sælges bedst.",
    ];
    k.HowTo =
    [
        "Log ind med samme kode som Lager.",
        "Vælg bunken: Skal vurderes eller Ingen pris. Øverst står, hvor mange der venter.",
        "Kig på billedet og det, der blev fundet. Tryk Ok for forslaget – eller skriv din egen pris og tryk Gem.",
        "Ved du det ikke? Tryk Spring over, så kommer den næste.",
        "Ser bogen ulæst ud? Tryk Som ny – ellers Brugt. Ovenfor kan du vise kun Som ny eller kun Brugt.",
        "Under forslaget står, hvad den koster ny hos Bog & idé (eller Fantask), så du kan sammenligne. Bøger prissættes kun efter danske sider.",
        "Dødsbo og loft: tryk Dødsbo og loft øverst, lav en sag (fx Mariannes ting), tag et billede af hver ting og tryk Vurdér de nye. Op til 8 billeder vurderes ad gangen. Ret prisen selv, hvis du ved bedre – og brug knapperne Søg på DBA og Søg på Lauritz for at se, hvad lignende ting koster.",
    ];
    k.Version = "2026.11.10.2";
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

var dodsboBody = File.ReadAllText(Path.Combine(app.Environment.WebRootPath, "dodsbo.html"));

app.MapGet("/", (KolibriAppInfo info, HttpContext ctx) => Page(info, ctx, body, "/app.js?v=2"));
// Dødsbo og loft (2026-11-10): the same login, its own page.
app.MapGet("/dodsbo", (KolibriAppInfo info, HttpContext ctx) => Page(info, ctx, dodsboBody, "/dodsbo.js?v=2"));

IResult Page(KolibriAppInfo info, HttpContext ctx, string pageBody, string script)
{
    ctx.Response.Headers.CacheControl = "no-cache";
    var owner = IsOwner(ctx);
    return Results.Content($"""
        <!doctype html><html lang="da" data-family="{info.Family}"><head>
        <script>document.documentElement.setAttribute('data-theme', localStorage.getItem('vurdering_theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));</script>
        <meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
        <title>Vurdering · Bogshoppen</title><meta name="description" content="{info.Tagline}"><meta name="theme-color" content="{info.ThemeColor}">
        <meta name="robots" content="noindex">
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css?v=5"><link rel="stylesheet" href="/app.css?v=2">
        <script src="/_content/ITMartin.Shared.UI/kolibri.js" defer></script><script src="{script}" defer></script>
        <link rel="manifest" href="/manifest.webmanifest"><link rel="apple-touch-icon" href="/{info.IconPath.TrimStart('/')}"></head>
        <body>{KolibriStaticPages.Header(info, actions: Buttons + (owner ? Logout : ""))}{(owner ? Steps() : "")}<main class="k-main"><div class="k-content">{(owner ? pageBody : LoginBody)}</div></main>{KolibriStaticPages.Footer(info)}</body></html>
        """, "text/html; charset=utf-8");
}

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

api.MapGet("/naeste", async (string? bunke, string? spring, string? brug, ReviewUseCases review, CancellationToken ct) =>
{
    var skip = (spring ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToList();
    return await review.NextAsync(bunke, skip, ct, brug) is { } q ? Results.Ok(q) : Results.Problem("Lager svarer ikke lige nu – prøv igen om lidt.", statusCode: 503);
});
api.MapPost("/brug", async (BrugForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.UsageAsync(f.Id, f.Brug, ct)));

api.MapPost("/ok", async (OkForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.OkAsync(f.Id, f.Forslag, ct)));
api.MapPost("/pris", async (PrisForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.OwnAsync(f.Id, f.Pris, ct)));
api.MapGet("/foto/{id:int}", async (int id, ReviewUseCases review, CancellationToken ct) =>
    await review.PhotoAsync(id, ct) is { } bytes ? Results.File(bytes, "image/jpeg") : Results.NotFound());

// ── Dødsbo og loft ──
api.MapGet("/dodsbo", (DodsboUseCases d) => Results.Ok(d.All().Select(s => new
{
    s.Id, s.Name, Ting = s.Ting.Count, Venter = s.Ting.Count(t => t.Waiting), Vaerdi = DodsboRules.Range(s.Total().Low, s.Total().High),
})));
api.MapPost("/dodsbo", (NySag f, DodsboUseCases d) => d.New(f.Navn) is var (s, error) && s is not null ? Results.Ok(new { s.Id }) : Results.BadRequest(new { fejl = error }));
api.MapGet("/dodsbo/{id}", (string id, DodsboUseCases d) => d.Get(id) is { } s ? Results.Ok(SagView(s)) : Results.NotFound());
api.MapPost("/dodsbo/{id}/foto", async (string id, HttpRequest req, DodsboUseCases d) =>
{
    var form = await req.ReadFormAsync();
    if (form.Files.GetFile("foto") is not { Length: > 0 and <= 15 * 1024 * 1024 } file) return Results.BadRequest(new { fejl = "Vælg et billede (højst 15 MB)." });
    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    return Done(d.AddPhoto(id, ms.ToArray(), form["hint"]));
}).DisableAntiforgery();
api.MapPost("/dodsbo/{id}/vurder", async (string id, DodsboUseCases d, CancellationToken ct) => Done(await d.ValueWaitingAsync(id, ct)));
api.MapPost("/dodsbo/{id}/ting/{tid}/egen", (string id, string tid, EgenForm f, DodsboUseCases d) =>
    Done(d.Own(id, tid, int.TryParse((f.Kr ?? "").Replace(".", "").Replace("kr", "").Trim(), out var kr) ? kr : null, f.Note)));
api.MapPost("/dodsbo/{id}/ting/{tid}/fjern", (string id, string tid, DodsboUseCases d) => Done(d.Remove(id, tid)));
api.MapGet("/dodsbo/{id}/foto/{tid}", (string id, string tid, DodsboUseCases d) =>
    d.Photo(id, tid) is { } jpeg ? Results.File(jpeg, "image/jpeg") : Results.NotFound());

static IResult Done(DodsboResult r) => r.Ok ? Results.Ok(new { besked = r.Message }) : Results.BadRequest(new { fejl = r.Message });

static object SagView(Sag s)
{
    var (low, high) = s.Total();
    return new
    {
        s.Id, s.Name, Vaerdi = DodsboRules.Range(low, high), Venter = s.Ting.Count(t => t.Waiting),
        Ting = s.Ting.OrderByDescending(t => t.AddedAt).Select(t => new
        {
            t.Id, t.Hint, Venter = t.Waiting, t.What, t.Details, Pris = DodsboRules.Range(t.LowKr, t.HighKr),
            Dom = t.Verdict is { } v ? DodsboRules.Text(v) : "", DomKode = t.Verdict?.ToString() ?? "", t.Where, t.Reason, t.SearchWords, t.Story,
            Egen = t.OwnKr, t.OwnNote,
        }),
    };
}

static IResult Answer(ReviewResult r) => r.Ok ? Results.Ok(new { besked = r.Message }) : Results.BadRequest(new { fejl = r.Message });

app.Run();

record OkForm(int Id, int? Forslag);
record PrisForm(int Id, string? Pris);
record BrugForm(int Id, string? Brug);
record NySag(string? Navn);
record EgenForm(string? Kr, string? Note);
