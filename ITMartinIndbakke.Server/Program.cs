using System.Security.Cryptography;
using System.Text;
using ITMartin.Shared.UI.Kolibri;
using ITMartinIndbakke.Application;
using ITMartinIndbakke.Domain;
using ITMartinIndbakke.Infrastructure;

// Indbakke (Kolibri Kommunikere) - 2026-11-04, user: "Emails... Cannot get any overview of them.. A lot need to be unsubscribed
// but it takes too much time... What is urgent + what do I need to reply to? ... like my filesorter cleans up cloud storage."
// + "a lot of Digital post are important" + "what is actually urgent now and how do I remember" (AKA / Jobnet / kommune).
// v1: scan (read-only, headers only) and show. Unsubscribe/archive presses come in v2.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Indbakke";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Ryd op i din mail – se hvad der haster, og hvad du skal svare på.";
    k.About =
    [
        "Indbakke giver dig overblik over din mail, ligesom FileSorter rydder op i billeder: hvad der haster, hvem der venter på svar, og hvem der bare fylder.",
        "Digital Post og breve fra a-kasse, Jobnet og kommune bliver i Det haster nu, til du selv trykker Klaret. Har brevet en frist, skriver du datoen, så kan du ikke glemme den.",
        "Indbakke læser kun afsender, emne og dato – aldrig selve teksten i dine mails. Den sletter og flytter intet.",
    ];
    k.HowTo =
    [
        "Tryk på Scan min mail. Det tager et par minutter første gang.",
        "Øverst står Det haster nu. Når du har læst brevet, trykker du Læst og skriver fristen, hvis der er en. Når det er ordnet, trykker du Klaret – eller Alt før i dag er klaret, så forsvinder alt det gamle, og kun nye breve kommer frem.",
        "Under Skal svares står mails fra rigtige mennesker, som du ikke har svaret på endnu.",
        "Under Afsendere kan du se, hvem der sender mest. Står der Kan afmeldes, kan du snart afmelde med ét tryk.",
    ];
    k.Version = "2026.11";
    k.ThemeColor = "#0b1220";
    k.StaticPages = true;
});

var dataDir = builder.Configuration["Indbakke:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
builder.Services.AddIndbakkeApplication();
builder.Services.AddIndbakkeInfrastructure(
    builder.Configuration["Indbakke:User"] ?? "",
    builder.Configuration["Indbakke:AppPassword"] ?? "",
    dataDir);

var app = builder.Build();
app.MapKolibri();
app.UseStaticFiles();

// Only Martin: PIN (Indbakke__Pin in magic.env); the cookie holds a hash, never the PIN (same as Kanal).
var pin = app.Configuration["Indbakke:Pin"] ?? "";
string? ownerToken = pin.Length >= 4 ? Token(pin) : null;
static string Token(string p) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("indbakke|" + p.Trim())))[..32];
bool IsOwner(HttpContext ctx) => ownerToken is not null && ctx.Request.Cookies.TryGetValue("indbakke_auth", out var v) && v == ownerToken;

var body = File.ReadAllText(Path.Combine(app.Environment.WebRootPath, "side.html"));
const string LoginBody = """
    <section class="k-card" data-nofold>
      <h1>Indbakke</h1>
      <p class="k-help">Skriv din kode for at se din mail.</p>
      <form method="post" action="/ind" class="ib-login">
        <label class="k-label" for="pin">Kode</label>
        <input class="k-input" id="pin" name="pin" type="password" autocomplete="current-password" required>
        <button class="k-btn k-btn-primary" type="submit">Log ind</button>
      </form>
    </section>
    """;
const string ThemeButton = """<button id="themeToggleBtn" class="k-btn k-btn-ghost k-btn-sm" type="button" title="Skift lys/mørk visning">Lys/mørk</button>""";

app.MapGet("/", (KolibriAppInfo info, HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-cache";
    var owner = IsOwner(ctx);
    return Results.Content($"""
        <!doctype html><html lang="da" data-family="{info.Family}"><head>
        <script>document.documentElement.setAttribute('data-theme', localStorage.getItem('indbakke_theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));</script>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
        <title>Indbakke</title><meta name="description" content="{info.Tagline}"><meta name="theme-color" content="{info.ThemeColor}">
        <meta name="robots" content="noindex">
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css?v=4"><link rel="stylesheet" href="/app.css?v=2">
        <script src="/_content/ITMartin.Shared.UI/kolibri.js" defer></script><script src="/app.js?v=3" defer></script>
        <link rel="manifest" href="/manifest.webmanifest"><link rel="apple-touch-icon" href="/{info.IconPath.TrimStart('/')}"></head>
        <body>{KolibriStaticPages.Header(info, actions: ThemeButton)}<main class="k-main"><div class="k-content">{(owner ? body : LoginBody)}</div></main>{KolibriStaticPages.Footer(info)}</body></html>
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
    ctx.Response.Cookies.Append("indbakke_auth", ownerToken, new CookieOptions { HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365) });
    return Results.Redirect("/");
});

var api = app.MapGroup("/api").AddEndpointFilter(async (ctx, next) =>
    IsOwner(ctx.HttpContext) ? await next(ctx) : Results.Unauthorized());

api.MapPost("/scan", (int? maaneder, MailScanner scanner) =>
    !scanner.IsConfigured ? Results.Ok(new { fejl = "Gmail er ikke sat op endnu (Indbakke__User og Indbakke__AppPassword)." })
    : Results.Ok(new { startet = scanner.Start(maaneder ?? 12) }));

api.MapGet("/status", (MailScanner s) => Results.Ok(new { koerer = s.Running, faerdige = s.Done, ialt = s.Total, fejl = s.Error, opsat = s.IsConfigured, harOversigt = s.Last is not null }));

api.MapGet("/oversigt", (MailScanner s, FollowUps follow, TimeProvider clock) =>
{
    if (s.Last is not { } o) return Results.Ok(new { tom = true });
    var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
    object Mail(MailHeader m, MailCategory c) => new
    {
        id = m.MessageId, fra = string.IsNullOrWhiteSpace(m.FromName) ? m.FromAddress : m.FromName, adresse = m.FromAddress,
        domaene = m.Domain, emne = m.Subject, dato = m.Date, laest = m.Seen, kategori = c.ToString(),
    };
    return Results.Ok(new
    {
        grundlinje = follow.Baseline,
        scannet = o.ScannedAt, maaneder = o.Months, ialt = o.Total, ulaeste = o.Unread,
        kategorier = o.PerCategory.ToDictionary(k => k.Key.ToString(), k => k.Value),
        hasterNu = follow.Now().Select(i => new
        {
            mail = Mail(i.Mail, i.Category), status = i.FollowUp.Status.ToString(), frist = i.FollowUp.Deadline,
            dageTilbage = i.DaysLeft(today), note = i.FollowUp.Note,
        }),
        skalSvares = o.NeedsReply.Select(x => Mail(x.Mail, x.Category)),
        afsendere = o.Senders.Take(300).Select(g => new
        {
            adresse = g.Address, navn = g.Name, antal = g.Count, ulaeste = g.Unread, nyeste = g.Newest,
            kategori = g.Category.ToString(), kanAfmeldes = g.CanUnsubscribe, etTryk = g.OneClick,
        }),
    });
});

api.MapPost("/laest", (FollowUpForm f, FollowUps follow) => Answer(follow.MarkRead(f.Id, f.Frist, f.Note)));
api.MapPost("/klaret", (FollowUpForm f, FollowUps follow) => Answer(follow.MarkDone(f.Id)));
api.MapPost("/grundlinje", (FollowUps follow) => { follow.SetBaseline(); return Results.Ok(new { ok = true }); });
api.MapPost("/grundlinje/fortryd", (FollowUps follow) => { follow.ClearBaseline(); return Results.Ok(new { ok = true }); });
api.MapPost("/fortryd", (FollowUpForm f, FollowUps follow) => Answer(follow.Reopen(f.Id)));
static IResult Answer(string? error) => error is null ? Results.Ok(new { ok = true }) : Results.BadRequest(new { fejl = error });

// After a restart the overview (in memory only) is gone - scan again by itself, so the page is never empty.
app.Lifetime.ApplicationStarted.Register(() =>
{
    var scanner = app.Services.GetRequiredService<MailScanner>();
    if (scanner.IsConfigured) scanner.Start(12);
});

app.Run();

record FollowUpForm(string Id, DateOnly? Frist, string? Note);
