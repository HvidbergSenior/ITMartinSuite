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
        "Skal vurderes = fundet til 50 kr eller mere, eller markeret som 💎 Sjælden, men uden din pris. Ingen pris = intet fundet nogen steder. De sjældne kommer først.",
        "Sjælden: skriv en kommentar (fx \"julehæfte – kalenderen er med\"), sæt din pris, tryk Til auktion (åbner varen i Lager) – eller Ikke sjælden, så går den tilbage til sin normale pris.",
        "Dødsbo og loft: tag et billede af hver ting fra et dødsbo, et loft eller en kælder. AI'en fortæller, hvad det er og historien bag det, hvad det cirka er værd brugt i Danmark, og om du skal sælge det, sælge det samlet, give det væk, smide det ud eller spørge en ekspert – og hvor det sælges bedst.",
    ];
    k.HowTo =
    [
        "Log ind med samme kode som Lager.",
        "Vælg bunken: Skal vurderes eller Ingen pris. Øverst står, hvor mange der venter.",
        "Kig på billedet og det, der blev fundet. Tryk Ok for forslaget – eller skriv din egen pris og tryk Gem.",
        "Står der en pris på selve tingen (et prismærke), læses den med billedet: Der står 69 kr på den. Tryk Brug prisen på mærket for at bruge den.",
        "Ved du det ikke? Tryk Spring over, så kommer den næste.",
        "Guld, sølv og bronze: de dyreste blade, bøger, film, plader og kort på DBA (hver uge) – Super diamant fra 5.000 kr, Guld fra 500, Sølv fra 200, Bronze fra 100. Kun numre og titler, som mindst 2 annoncer beder om. Ting på listen får mærket af sig selv.",
        "Ny i kiosken: skriv hvad det nyeste nummer af et blad koster nyt i dag (fx Anders And & Co. 40 kr). Samme navn igen retter prisen.",
        "Find en ting: skriv nummeret (fx 3372) eller titlen (fx X-Force) øverst og tryk Find – så kommer netop den ting frem. Spring over viser den næste, der passer. »Tilbage til bunken« går tilbage.",
        "Kun sikre læsninger kommer her: er AI'en usikker (fx kun en halv ryg), rettes tingen først i Lager under »Ret de svære«. »Kassér« fjerner en ting, der ikke er en rigtig ting eller er for dårligt læst – den gemmes i Lager som Ikke købt, og »Fortryd« henter den tilbage.",
        "Billedet: »Ret udsnittet« viser hele billedet af bunken – træk en firkant rundt om tingen, så bliver det dens nye billede. »Fjern prismærket« – træk en firkant hen over mærket med fingeren og tryk »Mal over mærket«; det males over i tingens egen farve. »Fortryd« giver det gamle billede tilbage. »Skjul billedet« – et dårligt billede vises ikke på bogshoppen.dk (tingen er stadig til salg), indtil du trykker »Vis billedet igen«.",
        "Find priser nu: Lager spørger alle prissiderne igen for netop den ting (tager op til et minut) – fx når du har rettet titlen eller ISBN i Lager. Priserne sættes kun her, ikke i Lager.",
        "Ser bogen ulæst ud? Tryk Som ny – ellers Brugt. Ovenfor kan du vise kun Som ny eller kun Brugt.",
        "Under forslaget står, hvad den koster ny hos Bog & idé (eller Fantask), så du kan sammenligne. Bøger prissættes kun efter danske sider.",
        "Dødsbo og loft: tryk Dødsbo og loft øverst, lav en sag (fx Mariannes ting), tag et billede af hver ting og tryk Vurdér de nye. Op til 8 billeder vurderes ad gangen. Ret prisen selv, hvis du ved bedre – og brug knapperne Søg på DBA og Søg på Lauritz for at se, hvad lignende ting koster.",
    ];
    k.Version = "2026.11.16";
    k.ThemeColor = "#0b1220";
    k.StaticPages = true;
});
builder.Services.AddVurderingApplication();
builder.Services.AddVurderingInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ITMartinVurdering.Server.DescribeJob>();   // Ting/Strik descriptions (2026-11-10)

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
      <h1 class="k-card-title">Vurdering</h1>
      <p class="k-help">Skriv koden – den samme som til Lager.</p>
      <form method="post" action="/ind" class="vu-login">
        <label class="k-label" for="pin">Kode</label>
        <input class="k-input" id="pin" name="pin" type="password" autocomplete="current-password" required>
        <button class="k-btn k-btn-primary" type="submit">Log ind</button>
      </form>
    </section>
    """;
const string Buttons = """<button id="themeToggleBtn" class="k-btn k-btn-ghost k-btn-sm" type="button" title="Skift lys/mørk visning">Lys/mørk</button>""";
const string Logout = """<form method="post" action="/ud" style="display:inline"><button class="k-btn k-btn-ghost k-btn-sm" type="submit">Log ud</button></form>""";

// The apps' tabs (KolibriSteps markup, 2026-11-10) - Priser is ours.
static string Steps()
{
    var sb = new StringBuilder("""<nav class="k-steps" aria-label="Bogshoppens apps"><ol>""");
    for (var i = 0; i < BogshoppenFlow.Steps.Count; i++)
    {
        var (name, url) = BogshoppenFlow.Steps[i];
        var e = System.Net.WebUtility.HtmlEncode(name);
        sb.Append(i + 1 == BogshoppenFlow.Priser
            ? $"""<li class="is-here"><span aria-current="page">{e}</span></li>"""
            : $"""<li><a href="{url}">{e}</a></li>""");
    }
    sb.Append("</ol>");
    // Forrige / Næste, the same as KolibriSteps (2026-11-15).
    var (pn, pu) = BogshoppenFlow.Steps[BogshoppenFlow.Priser - 2];
    var (nn, nu) = BogshoppenFlow.Steps[BogshoppenFlow.Priser];
    sb.Append($"""<div class="k-steps-nav"><a href="{pu}">← {System.Net.WebUtility.HtmlEncode(pn)}</a><a class="is-next" href="{nu}">{System.Net.WebUtility.HtmlEncode(nn)} →</a></div>""");
    return sb.Append("</nav>").ToString();
}

var dodsboBody = File.ReadAllText(Path.Combine(app.Environment.WebRootPath, "dodsbo.html"));
var guldBody = File.ReadAllText(Path.Combine(app.Environment.WebRootPath, "guld.html"));
var kioskBody = File.ReadAllText(Path.Combine(app.Environment.WebRootPath, "kiosk.html"));

app.MapGet("/", (KolibriAppInfo info, HttpContext ctx) => Page(info, ctx, body, "/app.js?v=13"));
// Guld/sølv/bronze + Ny i kiosken (2026-11-15, moved from Lager): the same login, own pages.
app.MapGet("/guld", (KolibriAppInfo info, HttpContext ctx) => Page(info, ctx, guldBody, "/guld.js?v=2"));
app.MapGet("/kiosk", (KolibriAppInfo info, HttpContext ctx) => Page(info, ctx, kioskBody, "/kiosk.js?v=1"));
// Dødsbo og loft (2026-11-10): the same login, its own page.
app.MapGet("/dodsbo", (KolibriAppInfo info, HttpContext ctx) => Page(info, ctx, dodsboBody, "/dodsbo.js?v=4"));
// Vareinfo's Tjek -> "Møbler og ting" (2026-11-10): straight into today's Tjek Sag, made by itself.
app.MapGet("/dodsbo/i-dag", (KolibriAppInfo info, HttpContext ctx, DodsboUseCases d) => IsOwner(ctx)
    ? Results.Redirect("/dodsbo?sag=" + Uri.EscapeDataString(d.Today().Id))
    : Page(info, ctx, dodsboBody, "/dodsbo.js?v=4"));   // the login, here - after it the same address again

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
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css?v=6"><link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri-rolig.css?v=3"><link rel="stylesheet" href="/app.css?v=9">
        <script src="/_content/ITMartin.Shared.UI/kolibri.js" defer></script><script src="{script}" defer></script>
        <link rel="manifest" href="/manifest.webmanifest"><link rel="apple-touch-icon" href="/{info.IconPath.TrimStart('/')}"></head>
        <body>{KolibriStaticPages.Header(info, title: BogshoppenFlow.Title(BogshoppenFlow.Priser), actions: Buttons + (owner ? Logout : ""))}{(owner ? Steps() : "")}<main class="k-main"><div class="k-content">{(owner ? pageBody : LoginBody)}</div></main>{KolibriStaticPages.Footer(info)}</body></html>
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
    // Back to the page the login was shown on (fx /dodsbo/i-dag from Vareinfo) - only an address on this site.
    var back = Uri.TryCreate(ctx.Request.Headers.Referer.ToString(), UriKind.Absolute, out var r) && r.Host == ctx.Request.Host.Host
        && r.AbsolutePath.StartsWith("/dodsbo") ? r.PathAndQuery : "/";
    return Results.Redirect(back);
}).DisableAntiforgery();

app.MapPost("/ud", (HttpContext ctx) => { ctx.Response.Cookies.Delete("vurdering_auth"); return Results.Redirect("/"); }).DisableAntiforgery();

var api = app.MapGroup("/api").AddEndpointFilter(async (ctx, next) => IsOwner(ctx.HttpContext) ? await next(ctx) : Results.Unauthorized());

api.MapGet("/naeste", async (string? bunke, string? spring, string? brug, ReviewUseCases review, CancellationToken ct) =>
{
    var skip = (spring ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToList();
    return await review.NextAsync(bunke, skip, ct, brug) is { } q ? Results.Ok(q) : Results.Problem("Lager svarer ikke lige nu – prøv igen om lidt.", statusCode: 503);
});
api.MapGet("/soeg", async (string? q, string? spring, ReviewUseCases review, CancellationToken ct) =>
{
    var skip = (spring ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToList();
    return await review.SearchAsync(q, skip, ct) is { } r ? Results.Ok(r) : Results.Problem("Lager svarer ikke lige nu – prøv igen om lidt.", statusCode: 503);
});
api.MapPost("/brug", async (BrugForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.UsageAsync(f.Id, f.Brug, ct)));

api.MapPost("/ok", async (OkForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.OkAsync(f.Id, f.Forslag, ct)));
api.MapPost("/pris", async (PrisForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.OwnAsync(f.Id, f.Pris, ct)));
api.MapPost("/find", async (FindForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.FindPricesAsync(f.Id, ct)));
api.MapPost("/sjaelden", async (SjaeldenForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.RareAsync(f.Id, f.Sjaelden, ct)));
api.MapPost("/kasser", async (FindForm f, ReviewUseCases review, CancellationToken ct) =>
    await review.DiscardAsync(f.Id, ct) is { } was ? Results.Ok(new { besked = "Kasseret – den er ikke på lager længere.", was })
    : Results.BadRequest(new { fejl = "Lager kunne ikke kassere den – prøv igen." }));
api.MapPost("/kasser/fortryd", async (FortrydForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.UndoDiscardAsync(f.Id, f.Was, ct)));
api.MapPost("/udsnit", async (MaerkeForm f, ReviewUseCases review, CancellationToken ct) =>
    Answer(await review.RecutAsync(f.Id, PhotoArea.From([f.Venstre, f.Top, f.Hoejre, f.Bund], 0.02), ct)));
api.MapGet("/bunkefoto/{id:int}", async (int id, ReviewUseCases review, CancellationToken ct) =>
    await review.PileAsync(id, ct) is { } jpeg ? Results.File(jpeg, "image/jpeg") : Results.NotFound());
api.MapPost("/skjulfoto", async (SkjulForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.PhotoHiddenAsync(f.Id, f.Skjul, ct)));
api.MapPost("/maerke", async (MaerkeForm f, ReviewUseCases review, CancellationToken ct) =>
    Answer(await review.CoverMarkAsync(f.Id, PhotoArea.From([f.Venstre, f.Top, f.Hoejre, f.Bund], 0.005), ct)));
api.MapPost("/maerke/fortryd", async (FindForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.UncoverAsync(f.Id, ct)));
api.MapPost("/note", async (NoteForm f, ReviewUseCases review, CancellationToken ct) => Answer(await review.NoteAsync(f.Id, f.Note, ct)));
api.MapGet("/guld", async (PriceListUseCases lists, CancellationToken ct) =>
    await lists.GoldAsync(ct) is { } g ? Results.Ok(g) : Results.Problem("Lager svarer ikke lige nu – prøv igen om lidt.", statusCode: 503));
api.MapGet("/kiosk", async (PriceListUseCases lists, CancellationToken ct) =>
    await lists.KioskAsync(ct) is { } k ? Results.Ok(k) : Results.Problem("Lager svarer ikke lige nu – prøv igen om lidt.", statusCode: 503));
api.MapPost("/kiosk", async (KioskForm f, PriceListUseCases lists, CancellationToken ct) => Answer(await lists.SaveKioskAsync(f.Blad, f.Pris, f.Hvorfra, ct)));
api.MapPost("/kiosk/{id:int}/slet", async (int id, PriceListUseCases lists, CancellationToken ct) => Answer(await lists.DeleteKioskAsync(id, ct)));
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

// Del med ejeren: Martin makes / changes / stops the link; the page itself is open to whoever has the link (read-only).
api.MapPost("/dodsbo/{id}/del", (string id, DelForm f, DodsboUseCases d, HttpRequest req) =>
    d.Share(id, f.Priser) is { } token ? Results.Ok(new { url = $"{req.Scheme}://{req.Host}/del/{token}" }) : Results.NotFound());
api.MapPost("/dodsbo/{id}/stopdel", (string id, DodsboUseCases d) => { d.StopSharing(id); return Results.Ok(new { besked = "Linket virker ikke længere." }); });

app.MapGet("/del/{token}", (string token, DodsboUseCases d, KolibriAppInfo info, HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    if (d.Shared(token) is not { } s)
        return Results.Content(SharePage(info, "Linket virker ikke", "<section class=\"k-card\" data-nofold><h1 class=\"k-card-title\">Linket virker ikke længere</h1><p>Spørg den, der sendte det, om et nyt.</p></section>"), "text/html; charset=utf-8", statusCode: 404);
    return Results.Content(SharePage(info, s.Name, ShareBody(s, token)), "text/html; charset=utf-8");
});
app.MapGet("/del/{token}/foto/{tid}", (string token, string tid, DodsboUseCases d) =>
    d.Shared(token) is { } s && s.Ting.Any(t => t.Id == tid) && d.Photo(s.Id, tid) is { } jpeg ? Results.File(jpeg, "image/jpeg") : Results.NotFound());

static string H(string? x) => System.Net.WebUtility.HtmlEncode(x ?? "");

static string ShareBody(Sag s, string token)
{
    var sb = new StringBuilder();
    sb.Append($"""
        <section class="k-card" data-nofold>
          <h1 class="k-card-title">{H(s.Name)}</h1>
          <p>Her er tingene – hvad de er, og historien bag dem. Tryk på et billede for at se det stort.</p>
          {(s.SharePrices && s.Total() is var (lo, hi) && hi > 0 ? $"<p><b>Det, der er værd at sælge, er samlet cirka {H(DodsboRules.Range(lo, hi))}</b> – et skøn, ikke en pris.</p>" : "")}
        </section>
        """);
    foreach (var t in s.Ting.Where(t => !t.Waiting).OrderBy(t => t.AddedAt))
    {
        var price = !s.SharePrices ? "" : t.OwnKr is > 0 ? DodsboRules.Range(t.OwnKr, t.OwnKr) : DodsboRules.Range(t.LowKr, t.HighKr);
        var photo = $"/del/{token}/foto/{t.Id}";
        sb.Append($"""
            <section class="k-card db-share" data-nofold>
              <a href="{photo}" target="_blank" rel="noopener"><img class="db-share-photo" src="{photo}" alt="{H(t.What)}" loading="lazy"></a>
              <h2 class="k-card-title">{H(t.What)}</h2>
              {(price.Length > 0 ? $"<p><b>Skøn: {H(price)}</b></p>" : "")}
              {(t.Story.Length > 0 ? $"<p>{H(t.Story)}</p>" : "")}
              {(t.Details.Length > 0 ? $"<p class=\"k-help\"><b>Det kan du kigge efter:</b> {H(t.Details)}</p>" : "")}
            </section>
            """);
    }
    if (s.Ting.Any(t => t.Waiting)) sb.Append("<p class=\"k-help\">Der kommer flere ting på, når de er kigget på.</p>");
    return sb.ToString();
}

static string SharePage(KolibriAppInfo info, string title, string pageBody) => $"""
    <!doctype html><html lang="da" data-family="{info.Family}"><head>
    <script>document.documentElement.setAttribute('data-theme', localStorage.getItem('vurdering_theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));</script>
    <meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
    <title>{H(title)}</title><meta name="robots" content="noindex, nofollow"><meta name="theme-color" content="{info.ThemeColor}">
    <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css?v=6"><link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri-rolig.css?v=3"><link rel="stylesheet" href="/app.css?v=9">
    <script src="/_content/ITMartin.Shared.UI/kolibri.js" defer></script><script src="/app.js?v=2" defer></script></head>
    <body>{KolibriStaticPages.Header(info, actions: Buttons)}<main class="k-main"><div class="k-content">{pageBody}</div></main>{KolibriStaticPages.Footer(info)}</body></html>
    """;

static IResult Done(DodsboResult r) => r.Ok ? Results.Ok(new { besked = r.Message }) : Results.BadRequest(new { fejl = r.Message });

static object SagView(Sag s)
{
    var (low, high) = s.Total();
    return new
    {
        s.Id, s.Name, Vaerdi = DodsboRules.Range(low, high), Venter = s.Ting.Count(t => t.Waiting),
        Delt = s.Shared ? $"/del/{s.ShareToken}" : "", s.SharePrices,
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
record FindForm(int Id);
record SjaeldenForm(int Id, bool Sjaelden);
record NoteForm(int Id, string? Note);
record SkjulForm(int Id, bool Skjul);
record FortrydForm(int Id, string? Was);
record MaerkeForm(int Id, double Venstre, double Top, double Hoejre, double Bund);
record KioskForm(string? Blad, string? Pris, string? Hvorfra);
record NySag(string? Navn);
record EgenForm(string? Kr, string? Note);
record DelForm(bool Priser);
