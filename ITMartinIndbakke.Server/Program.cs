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
        "Min Indbakke (betalt, efter aftale): log ind med dit nummer, din Gmail og en nøgle, du selv laver hos Google – så får du det hele med ét tryk: Det haster, Afmeld og Ryd alt det gamle. Nøglen gemmes aldrig; den glemmes, når du logger ud eller efter 30 minutter.",
        "Gratis for alle: en guide til at rydde op i din mail selv – Gmail, Outlook og Hotmail, Yahoo, iPhone og andre. Siden ser aldrig din mail; knapperne åbner bare din egen mail med en færdig søgning.",
        "Indbakke giver dig overblik over din mail, ligesom FileSorter rydder op i billeder: hvad der haster, hvem der venter på svar, og hvem der bare fylder.",
        "Digital Post og breve fra a-kasse, Jobnet og kommune bliver i Det haster nu, til du selv trykker Klaret. Har brevet en frist, skriver du datoen, så kan du ikke glemme den.",
        "Indbakke læser kun afsender, emne og dato – aldrig selve teksten i dine mails. Den sletter kun, når du selv trykker – og kun reklame, nyhedsbreve og notifikationer, aldrig mails fra mennesker, Digital Post eller kvitteringer.",
    ];
    k.HowTo =
    [
        "Gratis: tryk på din mail (fx Gmail eller Outlook), og følg trinene. Start med reklame – det er det letteste.",
        "Min Indbakke: lav nøglen (Vis mig hvordan – Gmail), skriv dit nummer, din Gmail og nøglen, og tryk Log ind. Telefonen kan huske nøglen til næste gang.",
        "Tryk på Scan min mail. Det tager et par minutter første gang.",
        "Øverst står Det haster nu. Når du har læst brevet, trykker du Læst og skriver fristen, hvis der er en. Når det er ordnet, trykker du Klaret – eller Alt før i dag er klaret, så forsvinder alt det gamle, og kun nye breve kommer frem.",
        "Under Venter på dit svar står mennesker, der har skrevet til dig, hvor du ikke har skrevet tilbage i samtalen – også hvis du svarede fra telefonen. Har du klaret det på en anden måde, så tryk Afsluttet. Fortryd ligger under Afsluttede.",
        "Under Ryd op kan du se, hvem der sender mest. Tryk Ryd op: du ser først, hvor mange mails det er, og hvilke der bliver gemt. Så kan du afmelde og slette med ét tryk.",
        "Under Ryd alt det gamle vælger du en dato og trykker Se hvad der sker. Du ser tallene først – så trykker du Ryd op nu. Alle mails får en Gmail-etiket, så Fortryd kan hente dem alle tilbage.",
        "Slettede mails ligger i Gmails papirkurv i 30 dage. Under Seneste oprydning kan du trykke Fortryd og få dem tilbage.",
    ];
    k.Version = "2026.11.4";
    k.ThemeColor = "#0b1220";
    k.StaticPages = true;
});

var dataDir = builder.Configuration["Indbakke:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
builder.Services.AddIndbakkeApplication();
builder.Services.AddIndbakkeInfrastructure(
    builder.Configuration["Indbakke:User"] ?? "",
    builder.Configuration["Indbakke:AppPassword"] ?? "",
    dataDir);

builder.Services.AddMinIndbakke(builder.Configuration, dataDir);   // Min Indbakke: Indbakke__Kunder = the numbers Martin gave access
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<OwnerGate>();
// Each request works on ONE mailbox: Martin's (PIN cookie) or the person's own (session cookie, key in memory only).
// Not logged in = Martin's object is bound but never used - the /api filter answers 401 first.
builder.Services.AddScoped(sp =>
{
    var ctx = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var owner = sp.GetRequiredService<OwnerMail>().Workspace;
    if (ctx is null || sp.GetRequiredService<OwnerGate>().IsOwner(ctx)) return owner;
    return sp.GetRequiredService<MailSessions>().Get(ctx.Request.Cookies[OwnerGate.SessionCookie]) ?? owner;
});
builder.Services.AddScoped(sp => sp.GetRequiredService<MailWorkspace>().Scanner);
builder.Services.AddScoped(sp => sp.GetRequiredService<MailWorkspace>().FollowUps);
builder.Services.AddScoped(sp => sp.GetRequiredService<MailWorkspace>().Cleanup);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();
app.MapKolibri();
app.UseStaticFiles();

// Only Martin: PIN (Indbakke__Pin in magic.env); the cookie holds a hash, never the PIN (same as Kanal).
var pin = app.Configuration["Indbakke:Pin"] ?? "";
string? ownerToken = pin.Length >= 4 ? Token(pin) : null;
static string Token(string p) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("indbakke|" + p.Trim())))[..32];
var gate = app.Services.GetRequiredService<OwnerGate>();
gate.Token = ownerToken;
bool IsOwner(HttpContext ctx) => gate.IsOwner(ctx);
var sessions = app.Services.GetRequiredService<MailSessions>();
MailWorkspace? Guest(HttpContext ctx) => IsOwner(ctx) ? null : sessions.Get(ctx.Request.Cookies[OwnerGate.SessionCookie]);

var body = File.ReadAllText(Path.Combine(app.Environment.WebRootPath, "side.html"));
// Everyone but Martin gets the free guide (user 2026-11-05, option 1) - their mail never reaches this server; his code is at the bottom.
var LoginBody = File.ReadAllText(Path.Combine(app.Environment.WebRootPath, "guide.html"));
const string LogoutButton = """<form method="post" action="/ud" style="display:inline"><button class="k-btn k-btn-ghost k-btn-sm" type="submit">Log ud</button></form>""";
const string ThemeButton = """<button id="themeToggleBtn" class="k-btn k-btn-ghost k-btn-sm" type="button" title="Skift lys/mørk visning">Lys/mørk</button>""";

app.MapGet("/", (KolibriAppInfo info, HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-cache";
    // ?guide=1: Martin sees the free guide like everyone else (user 2026-11-05: "I still have old" - he was logged in).
    var guest = Guest(ctx);
    var owner = (IsOwner(ctx) || guest is not null) && ctx.Request.Query["guide"] != "1";
    return Results.Content($"""
        <!doctype html><html lang="da" data-family="{info.Family}"><head>
        <script>document.documentElement.setAttribute('data-theme', localStorage.getItem('indbakke_theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));</script>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
        <title>{(owner ? "Indbakke" : "Ryd op i din mail · Indbakke")}</title><meta name="description" content="{info.Tagline}"><meta name="theme-color" content="{info.ThemeColor}">
        {(owner ? "<meta name=\"robots\" content=\"noindex\">" : "")}
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css?v=4"><link rel="stylesheet" href="/app.css?v=12">
        <script src="/_content/ITMartin.Shared.UI/kolibri.js" defer></script><script src="/app.js?v=11" defer></script>
        <link rel="manifest" href="/manifest.webmanifest"><link rel="apple-touch-icon" href="/{info.IconPath.TrimStart('/')}"></head>
        <body>{KolibriStaticPages.Header(info, actions: ThemeButton + (guest is not null || IsOwner(ctx) ? LogoutButton : ""))}<main class="k-main"><div class="k-content">{(owner ? (guest is null ? body : GuestBody(guest.Name)) : Guide(ctx.Request.Query["fejl"]))}</div></main>{KolibriStaticPages.Footer(info)}</body></html>
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

// Min Indbakke: kundenummer + Gmail + key, EVERY time (user 2026-11-05). The key goes into memory only; the cookie holds a random token.
app.MapPost("/min", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var r = await sessions.OpenAsync(form["kode"], form["mail"], form["noegle"], ctx.RequestAborted);
    if (r.Token is null)
    {
        await Task.Delay(600);
        return Results.Redirect("/?fejl=" + Uri.EscapeDataString(r.Error ?? "Det virkede ikke.") + "#min");
    }
    ctx.Response.Cookies.Append(OwnerGate.SessionCookie, r.Token, new CookieOptions { HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Strict });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapPost("/ud", (HttpContext ctx) =>
{
    sessions.Close(ctx.Request.Cookies[OwnerGate.SessionCookie]);
    ctx.Response.Cookies.Delete(OwnerGate.SessionCookie);
    ctx.Response.Cookies.Delete("indbakke_auth");   // Martin too (user 2026-11-05: "i cant logout")
    return Results.Redirect("/");
}).DisableAntiforgery();

var api = app.MapGroup("/api").AddEndpointFilter(async (ctx, next) =>
    IsOwner(ctx.HttpContext) || Guest(ctx.HttpContext) is not null ? await next(ctx) : Results.Unauthorized());

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
        skalSvares = follow.Waiting().Select(x => Mail(x.Mail, x.Category)),
        afsluttede = follow.Closed().Take(20).Select(x => Mail(x.Mail, x.Category)),
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
// v2 - Ryd op per sender: see first, then Afmeld and/or Slet (Papirkurv, 30 days), and Fortryd.
api.MapGet("/afsender", (string adresse, Cleanup cleanup) =>
{
    var p = cleanup.Preview(adresse);
    if (p is null) return Results.NotFound();
    return Results.Ok(new
    {
        adresse = p.Address, navn = p.Name, slettes = p.ToDelete.Count, aeldste = p.Oldest, eksempler = p.Examples,
        beholdes = p.Kept.Select(k => new { hvorfor = k.Why, antal = k.Count }),
        afmeld = p.Unsubscribe?.Kind.ToString(),
    });
});
api.MapPost("/afsender/ryd", async (RydForm f, Cleanup cleanup, CancellationToken ct) =>
{
    var r = await cleanup.RunAsync(f.Adresse ?? "", f.Afmeld, f.Slet, CancellationToken.None);   // a cut request must not stop a move halfway
    return r.Ok ? Results.Ok(new { besked = r.Message, flyttet = r.Moved, aabn = r.OpenPage }) : Results.BadRequest(new { fejl = r.Message });
});
// "Ryd alt det gamle": numbers first (Gmail searches only), then one press; label + Fortryd for the whole pile.
api.MapGet("/gammelt", async (DateOnly foer, OldMode maade, Cleanup cleanup, CancellationToken ct) =>
{
    var p = await cleanup.PreviewOldAsync(foer, maade, ct);
    return p is null ? Results.BadRequest(new { fejl = "Datoen ser forkert ud." })
        : Results.Ok(new { foer = p.Before, ialt = p.Total, papirkurv = p.ToTrash, arkiv = p.ToArchive, stjerne = p.Starred });
});
// Big jobs run in the background (Cloudflare cuts a request after 100 s); the page asks /job until it is done.
api.MapPost("/gammelt", (GammeltForm f, Cleanup cleanup) => Answer(cleanup.StartClearOld(f.Foer, f.Maade)));
api.MapGet("/job", (Cleanup cleanup) => Results.Ok(new { koerer = cleanup.JobRunning, besked = cleanup.JobMessage }));

api.MapGet("/oprydning", (Cleanup cleanup, TimeProvider clock) => Results.Ok(cleanup.History().Select(a => new
{
    id = a.Id, tid = a.At, navn = a.Name, adresse = a.Address, antal = a.MessageIds.Count + a.Trashed, arkiv = a.Archived, etiket = a.Label, afmeldt = a.Unsubscribed,
    fortrudt = a.Restored, kanFortrydes = a.CanUndo(clock.GetUtcNow()),
})));
api.MapPost("/oprydning/fortryd", (FortrydForm f, Cleanup cleanup) => Answer(cleanup.StartUndo(f.Id)));

static IResult Answer(string? error) => error is null ? Results.Ok(new { ok = true }) : Results.BadRequest(new { fejl = error });

// After a restart the overview (in memory only) is gone - scan again by itself, so the page is never empty.
app.Lifetime.ApplicationStarted.Register(() =>
{
    var scanner = app.Services.GetRequiredService<OwnerMail>().Workspace.Scanner;
    if (scanner.IsConfigured) scanner.Start(12);
});

app.Run();

// A failed Min Indbakke login comes back with ?fejl=: that card opens and says why.
string Guide(string? fejl) => string.IsNullOrWhiteSpace(fejl) ? LoginBody
    : LoginBody.Replace("<section class=\"k-card ib-min\" data-fold-closed id=\"min\">", "<section class=\"k-card ib-min\" id=\"min\">")
               .Replace("<!--FEJL-->", $"<p class=\"ib-fejl\" role=\"alert\">{System.Net.WebUtility.HtmlEncode(fejl)}</p>");

// The person's own Indbakke = the same page as Martin's, with whose mail it is on top and without Martin's guide link.
string GuestBody(string name) =>
    $"<p class=\"k-help k-center ib-who\">Min Indbakke · {System.Net.WebUtility.HtmlEncode(name)} · din nøgle glemmes, når du logger ud eller lukker siden i 30 minutter.</p>"
    + body.Replace("<p class=\"k-help k-center ib-guide-link\">", "<p hidden class=\"ib-guide-link\">");

record FollowUpForm(string Id, DateOnly? Frist, string? Note);
record RydForm(string? Adresse, bool Afmeld, bool Slet);
record FortrydForm(Guid Id);
record GammeltForm(DateOnly Foer, OldMode Maade);

/// <summary>Who is Martin (PIN cookie with a hash, never the PIN) - and the name of the Min Indbakke session cookie.</summary>
sealed class OwnerGate
{
    public const string SessionCookie = "indbakke_s";
    public string? Token { get; set; }
    public bool IsOwner(HttpContext ctx) => Token is not null && ctx.Request.Cookies.TryGetValue("indbakke_auth", out var v) && v == Token;
}
