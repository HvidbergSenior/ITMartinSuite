using ITMartinHjem.Application;
using ITMartinHjem.Domain;
using ITMartinHjem.Infrastructure;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ITMartin.Shared.UI.Kolibri;
using ITMartinHjem.Server;
using ITMartinHjem.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Martin Hvidberg";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Hvad jeg laver, hvad jeg tænker – og min familie.";
    k.About =
    [
        "Det her er min egen side. Her kan du følge, hvad jeg laver lige nu, og se tilbage i tiden: billeder, videoer, lyd og tekster.",
        "Arbejdet og projekterne er åbne for alle. Familiens billeder kræver familiens kodeord.",
        "Har du et spørgsmål, så skriv til mig nederst på siden. Er jeg ved skærmen, svarer jeg med det samme.",
    ];
    k.HowTo =
    [
        "Læs \"Lige nu\" øverst – det er det, jeg arbejder med.",
        "Rul ned gennem tidslinjen, eller fold et år ud.",
        "Tryk \"Skriv til Martin\" for at stille et spørgsmål.",
    ];
    k.Version = "2026.09";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
// itmartin.dk shows the chat in an iframe: framing is governed by the CSP below instead.
builder.Services.AddAntiforgery(o => o.SuppressXFrameOptionsHeader = true);
builder.Services.AddCors(o => o.AddPolicy("itmartin", p => p
    .WithOrigins("https://itmartin.dk", "https://www.itmartin.dk", "http://localhost:5199").WithMethods("GET")));
// Visit counting: every app on *.itmartin.dk and *.bogshoppen.dk posts its page views here (Domain/VisitRules).
builder.Services.AddCors(o => o.AddPolicy("track", p => p
    .SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var u)
        && VisitRules.DomainOf(u.Host) is not null)
    .WithMethods("POST").AllowAnyHeader()));
builder.Services.AddCascadingAuthenticationState();

var dataDir = builder.Configuration["Hjem:DataDir"] ?? "/app/data";
Directory.CreateDirectory(dataDir);
// All of Hjem is layered since 2026-10-20 (Domain / Application / Infrastructure); this project only has the pages and endpoints.
builder.Services.AddHjemApplication();
builder.Services.AddSingleton(new HjemOptions());
builder.Services.AddHjemInfrastructure($"Data Source={Path.Combine(dataDir, "hjem.db")}", new HjemInfraOptions
{
    DataDir = dataDir,
    ClaudeApiKey = builder.Configuration["Claude:ApiKey"],
    SmtpHost = builder.Configuration["Hjem:Smtp:Host"] ?? "send.one.com",
    SmtpPort = builder.Configuration.GetValue("Hjem:Smtp:Port", 587),
    SmtpUser = builder.Configuration["Hjem:Smtp:User"] ?? "info@itmartin.dk",
    SmtpPassword = builder.Configuration["Hjem:Smtp:Password"],
    SmtpReplyTo = builder.Configuration["Hjem:Smtp:ReplyTo"] ?? "ITMartin@Mensa.dk",
    KigMedStatus = builder.Configuration["Hjem:KigMedStatus"] ?? "http://10.0.0.200:9144/api/status",
    ElPriserSnapshot = builder.Configuration["Hjem:ElPriserSnapshot"] ?? "http://10.0.0.126:8120/api/snapshot?area=DK1&allIn=true",
    KunderUrl = builder.Configuration["Kunder:Url"],
    KunderApiKey = builder.Configuration["Kunder:ApiKey"],
}, builder.Configuration["Hjem:StemApi"]);
// The shop on bogshoppen.dk/varer (2026-10-15): Køb nu, orders and offers live in Bogshoppen Salg on the photoserver
// since 2026-10-21 (same /api/butik as Lager had - Hjem__SalgApi / Hjem__SalgShopKey, the old Lager names still work).
builder.Services.AddLagerShopGateway(builder.Configuration["Hjem:SalgApi"] ?? builder.Configuration["Hjem:LagerApi"],
    builder.Configuration["Hjem:SalgShopKey"] ?? builder.Configuration["Hjem:LagerShopKey"]);
builder.Services.AddSingleton(new ShopVisibility(builder.Configuration.GetValue<bool>("Hjem:ShowFound"), builder.Configuration["Hjem:ShopPreviewCode"] ?? ""));
builder.Services.AddHjemShop(new ShopSeller(builder.Configuration["Hjem:ShopCompany"] ?? "", builder.Configuration["Hjem:ShopCvr"] ?? "",
    builder.Configuration["Hjem:ShopAddress"] ?? "", builder.Configuration["Hjem:ShopEmail"] ?? "ITMartin@Mensa.dk",
    builder.Configuration["Hjem:ShopPhone"] ?? "31 19 47 30"));
builder.Services.AddHostedService<ChatSweeper>();   // reply mails/notifications, kept in the DB

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("hjem");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "hjem_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(180);
        o.SlidingExpiration = true;
        o.LoginPath = "/login";
    });
builder.Services.AddAuthorization();

var app = builder.Build();
// Behind the Cloudflare tunnel every request arrives as http; trust its X-Forwarded-Proto so
// redirects (/bliv-pilot -> /bliv-pilot/) and absolute links stay on https.
var fwd = new ForwardedHeadersOptions { ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto };
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);
app.MapKolibri();

await app.Services.UpgradeHjemTablesAsync();
await app.Services.GetRequiredService<ISettingsRepository>().EnsureAsync(Settings.FirstRun());
await app.Services.GetRequiredService<AppLinkUseCases>().SeedAsync();
await app.Services.GetRequiredService<VisitUseCases>().StartAsync();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

// Only this site and itmartin.dk may put these pages in a frame (the /chat bubble there).
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self' https://itmartin.dk https://www.itmartin.dk";
    await next();
});
// stats.itmartin.dk now only receives the apps' visit counts - the numbers themselves are in Svar.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Host.Host.StartsWith("stats.", StringComparison.OrdinalIgnoreCase) && !ctx.Request.Path.StartsWithSegments("/api"))
    {
        ctx.Response.Redirect("https://itmartin.dk/admin/tal");
        return;
    }
    await next();
});
app.UseDefaultFiles();   // /bliv-pilot/ and /rejsedemo/ -> their index.html
app.UseStaticFiles(new StaticFileOptions
{
    // widget.js is loaded by itmartin.dk: allow it, and keep the cache short so changes show up.
    OnPrepareResponse = c =>
    {
        if (c.File.Name == "widget.js") c.Context.Response.Headers.CacheControl = "public, max-age=300";
        // Hand-made pages (/bliv-pilot/ …) must always be checked: a stale copy (e.g. the old one.com page
        // without the menu) was reused on link clicks, so visitors could not navigate away.
        else if (c.File.Name.EndsWith(".html") || c.File.Name == "nav.js") c.Context.Response.Headers.CacheControl = "no-cache";
    }
});
// Routing AFTER static files: the /{*Slug} page route would otherwise answer app.css, hjem.js …
app.UseRouting();
app.UseCors();

// itmartin.dk/tael-ikke-mig also leaves a cookie for the whole *.itmartin.dk domain (user 2026-10-06: "filter my phone
// and machine out" - also in the apps). kolibri.js in every app reads it and marks that app's own browser id.
// Not HttpOnly on purpose: the apps' scripts must see it. It holds nothing but "1".
app.Use(async (ctx, next) =>
{
    // On bogshoppen.dk/tael-ikke-mig the cookie is for *.bogshoppen.dk (Lager) - a browser only takes its own domain.
    if (ctx.Request.Path.Equals("/tael-ikke-mig", StringComparison.OrdinalIgnoreCase) && VisitRules.DomainOf(ctx.Request.Host.Host) is { } domain)
        ctx.Response.Cookies.Append("ikke_mig", "1", new CookieOptions
        {
            Domain = domain, Path = "/", MaxAge = TimeSpan.FromDays(400),
            Secure = true, SameSite = SameSiteMode.Lax, HttpOnly = false, IsEssential = true,
        });
    await next();
});

// ── Live "Lige nu" for itmartin.dk (wwwroot/widget.js reads this) ───────────────────
app.MapGet("/api/nu", async (SiteUseCases site) =>
{
    var s = await site.SettingsAsync();
    var player = Embed.PlayerUrl(s.NowLink);
    var media = s.NowMediaName.Length == 0 ? null : new
    {
        type = s.NowMediaType.StartsWith("video/") ? "video" : "image",
        url = $"https://martin.itmartin.dk/nu/{s.NowMediaName}",
        thumb = s.NowMediaThumb.Length > 0 ? $"https://martin.itmartin.dk/nu/{s.NowMediaThumb}" : null,
    };
    return Results.Json(new
    {
        name = s.Name,
        intro = s.Intro,
        html = TinyMarkdown.Render(s.NowText).Value,
        updated = Dk.Local(s.NowUpdated).ToString("d. MMMM yyyy", new System.Globalization.CultureInfo("da-DK")),
        available = s.Available,
        player,
        link = player is null && s.NowLink.Length > 0 ? s.NowLink : null,
        media,
    });
}).RequireCors("itmartin");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Visit counting (Application/VisitUseCases; numbers on /admin/tal). Apps: the old snippet to stats.itmartin.dk/api/hit.
// The website itself: wwwroot/t.js, which also sends the time on the page.
app.MapPost("/api/hit", async (HttpContext ctx, HitRequest req, VisitUseCases visits) =>
    await visits.RecordAsync(req, ctx.Request.Headers.UserAgent.ToString(), ClientIp(ctx), ctx.User.IsOwner()) is { } id
        ? Results.Json(new { id }) : Results.NoContent())
    .DisableAntiforgery().RequireCors("track");

app.MapPost("/api/hit/{id:long}/tid", async (long id, HttpContext ctx, VisitUseCases visits) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    if (int.TryParse((await reader.ReadToEndAsync()).Trim(), out var seconds)) await visits.SetSecondsAsync(id, seconds);
    return Results.NoContent();
}).DisableAntiforgery().RequireCors("track");

// itmartin.dk/tael-ikke-mig: this browser is Martin's - never count it, and remove its earlier visits (t.js sends the id).
// Only the browser id is marked, never the IP: a mobile IP is shared with strangers.
// The apps (kolibri.js) call it too, with their own browser id, when they see the ikke_mig cookie below.
app.MapPost("/api/hit/mig", async (HitRequest req, VisitUseCases visits) => Results.Json(new { removed = await visits.MarkOwnerVisitorAsync(req.VisitorId) }))
    .DisableAntiforgery().RequireCors("track");

app.MapGet("/api/last-seen", async (VisitUseCases visits) => Results.Ok(await visits.LastSeenAsync()));

// ── Login: one small form, two kinds (familie = password, ejer = PIN) ───────────────
app.MapGet("/login", (string? who, string? err) => Results.Content(LoginPage(who == "ejer", err is not null), "text/html"));

app.MapPost("/login", async (HttpContext ctx, IConfiguration cfg, VisitUseCases visits) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var owner = form["who"] == "ejer";
    var expected = owner ? cfg["Hjem:AdminPin"] : cfg["Hjem:FamilyPassword"];
    var given = form["code"].ToString().Trim();
    if (string.IsNullOrEmpty(expected) || !SameText(given, expected))
    {
        await Task.Delay(800); // slow down guessing
        return Results.Redirect(owner ? "/login?who=ejer&err=1" : "/login?err=1");
    }
    var claims = new List<Claim> { new(ClaimTypes.Name, owner ? "Martin" : "Familie"), new(ClaimTypes.Role, Access.Family) };
    if (owner) claims.Add(new(ClaimTypes.Role, Access.Owner));
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties { IsPersistent = true });
    if (owner) await visits.MarkOwnerIpAsync(ClientIp(ctx));   // Martin's own visits are not counted from this IP
    return Results.Redirect(owner ? "/admin" : "/");
}).DisableAntiforgery();

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// ── "Lige nu" pictures/film (always public). The file name changes on every upload, so browsers may cache it.
app.MapGet("/nu/{name}", async (string name, HttpContext ctx, SiteUseCases site) =>
{
    if (await site.NowFileAsync(name) is not { } f) return Results.NotFound();
    // private: browsers may keep it, Cloudflare may not - a picture removed in Svar must disappear at once.
    ctx.Response.Headers.CacheControl = "private, max-age=86400";
    return Results.File(f.Path, f.ContentType, enableRangeProcessing: true);
});

// Kontakt was merged into "Om mig og kontakt" (2026-09-28) - old links land there.
// Shop photos come from Lager through here, so the buyer never talks to the photoserver.
app.MapGet("/varer/foto/{id:int}", async (int id, HttpContext ctx, ShopVisibility visibility, ShopUseCases shop) =>
    visibility.Visible(ctx.Request.Cookies[ShopVisibility.Cookie]) && await shop.PhotoAsync(id) is { } jpeg ? Results.File(jpeg, "image/jpeg") : Results.NotFound());
// Martin's preview of the shop while it is closed (user 2026-10-21: "i want to test it first"): /vis-butik?kode=... once
// on each phone/PC, /skjul-butik to see it as the public does. A wrong code just goes to the teaser.
app.MapGet("/vis-butik", (string? kode, HttpContext ctx, ShopVisibility visibility) =>
{
    if (visibility.CodeOk(kode))
        ctx.Response.Cookies.Append(ShopVisibility.Cookie, kode!, new CookieOptions
        {
            HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(60)
        });
    return Results.Redirect("/varer");
});
app.MapGet("/skjul-butik", (HttpContext ctx) => { ctx.Response.Cookies.Delete(ShopVisibility.Cookie); return Results.Redirect("/varer"); });

app.MapGet("/kontakt", () => Results.Redirect("/om-mig", permanent: true));
// The pilot page moved to /bliv-pilot/ (2026-09-29): browsers kept a stale one.com copy of /pilot/ without the menu.
app.MapGet("/pilot", () => Results.Redirect("/bliv-pilot", permanent: true));   // also answers /pilot/
app.MapPost("/pilot/send.php", () => Results.Redirect("/bliv-pilot/send.php", permanent: true, preserveMethod: true));
// Since 2026-09-30 /bliv-pilot is a normal page (text in Svar -> Sider + Components/PilotForm); /bliv-pilot/ lands there too.
app.MapGet("/rejsedemo", () => Results.Redirect("/rejsedemo/", permanent: true));

// ── The site menu for hand-made pages in wwwroot (pilot): wwwroot/nav.js draws it from this.
// Birthday day on the hand-made pages (wwwroot/nav.js draws the strip when this says on).
app.MapGet("/api/fest", (IConfiguration config) => Results.Ok(new { on = ITMartinHjem.Server.Components.Fest.IsOn(config) }));

app.MapGet("/api/menu", async (SiteUseCases site) =>
    Results.Json((await site.MenuAsync()).Select(x => new { title = x.Title, href = x.Href })));

// ── Kontrol ("🏠 På hjemmesiden") switches apps on/off on /mine-apps. Shared key Hjem__KontrolKey
// in magic.env - both containers read the same file.
bool KontrolKeyOk(HttpRequest req, IConfiguration cfg) =>
    cfg["Hjem:KontrolKey"] is { Length: > 10 } k && req.Headers["X-Hjem-Key"] == k;

app.MapGet("/api/kontrol/apps", async (HttpRequest req, IConfiguration cfg, AppLinkUseCases apps) =>
{
    if (!KontrolKeyOk(req, cfg)) return Results.Unauthorized();
    return Results.Json((await apps.AllWithStatusAsync())
        .Select(x => new { x.Link.Id, x.Link.Icon, x.Link.Name, x.Link.Url, x.Link.Description, x.Link.Show, x.Link.Awake, up = x.Up }));
});

app.MapPost("/api/kontrol/apps/{id:int}/awake/{awake:bool}", async (int id, bool awake, HttpRequest req, IConfiguration cfg, AppLinkUseCases apps) =>
    !KontrolKeyOk(req, cfg) ? Results.Unauthorized() : await apps.SetAwakeAsync(id, awake) ? Results.Ok() : Results.NotFound())
    .DisableAntiforgery();

app.MapPost("/api/kontrol/apps/{id:int}/show/{show:bool}", async (int id, bool show, HttpRequest req, IConfiguration cfg, AppLinkUseCases apps) =>
    !KontrolKeyOk(req, cfg) ? Results.Unauthorized() : await apps.SetShowAsync(id, show) ? Results.Ok() : Results.NotFound())
    .DisableAntiforgery();

// ── The pilot form (wwwroot/bliv-pilot/index.html posts to send.php, as it did on one.com).
// Instead of a mail it becomes a conversation in Svar, with a push to Martin's phone.
app.MapPost("/bliv-pilot/send.php", async (HttpContext ctx, ChatUseCases chat) =>
{
    var form = await ctx.Request.ReadFormAsync();
    string F(string k) => form[k].ToString().Trim();
    if (F("website").Length > 0) return Results.Redirect("/bliv-pilot");            // spam trap
    var (ydelse, navn, tlf, by, besked, email) = (F("ydelse"), F("navn"), F("telefon"), F("by"), F("besked"), F("email"));
    if (navn.Length == 0 || tlf.Length == 0 || ydelse.Length == 0) return Results.Redirect("/bliv-pilot#pilot");
    await chat.PilotRequestAsync(ydelse, navn, tlf, by, besked, email);
    Func<string?, string?> e = System.Net.WebUtility.HtmlEncode;
    return Results.Content($$"""
        <!doctype html><html lang="da"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Tak</title>
        <style>body{font:18px/1.5 system-ui,sans-serif;max-width:560px;margin:60px auto;padding:0 16px;color:#1d2a25}a{color:#1f7a5c}</style></head>
        <body><h1>Tak, {{e(navn)}}!</h1>
        <p>Jeg har fået din besked om <b>{{e(ydelse)}}</b> og ringer til dig på {{e(tlf)}} inden for to dage.</p>
        {{(ChatRules.LooksLikeEmail(email) ? $"<p>Du får en kvittering på <b>{e(email)}</b> – og mit svar kommer også dertil.</p>" : "")}}
        <p><a href="/bliv-pilot">Tilbage</a> · <a href="/">Til forsiden</a></p></body></html>
        """, "text/html");
}).DisableAntiforgery();

// ── Pictures on the pages (Om mig …): only files that a page actually uses.
app.MapGet("/f/{name}", async (string name, HttpContext ctx, SiteUseCases site) =>
{
    if (await site.PageFileAsync(name) is not { } f) return Results.NotFound();
    ctx.Response.Headers.CacheControl = "public, max-age=604800";
    return Results.File(f.Path, f.ContentType, enableRangeProcessing: true);
});

// ── Media: family files only with the family cookie ─────────────────────────────────
app.MapGet("/m/{id:int}/{size}", async (int id, string size, HttpContext ctx, SiteUseCases site) =>
{
    if (await site.TimelineFileAsync(id, size, ctx.User.IsFamily()) is not { } f) return Results.NotFound();
    ctx.Response.Headers.CacheControl = f.Private ? "private, max-age=86400" : "public, max-age=604800";
    return Results.File(f.Path, f.ContentType, f.DownloadName, enableRangeProcessing: true);
});

// The owner's home-screen icon: opens straight on /admin (the front page keeps Kolibri's manifest).
app.MapGet("/admin.webmanifest", () => Results.Json(new
{
    name = "Martin – svar og opslag",
    short_name = "Svar",
    start_url = "/admin",
    scope = "/",
    display = "standalone",
    background_color = "#f4f6f5",
    theme_color = "#f4f6f5",
    lang = "da",
    icons = new[] { new { src = "/icon-192.png", sizes = "192x192", type = "image/png" }, new { src = "/icon-512.png", sizes = "512x512", type = "image/png" } },
}, contentType: "application/manifest+json"));

// ── Push: only the owner subscribes (called from wwwroot/hjem.js) ───────────────────
app.MapGet("/api/push/public-key", (PushUseCases push) => Results.Text(push.PublicKey));

app.MapPost("/api/push/subscribe", async (SubRequest req, HttpContext ctx, PushUseCases push) =>
{
    if (!ctx.User.IsOwner()) return Results.Unauthorized();
    await push.SubscribeOwnerAsync(req.Endpoint, req.P256dh, req.Auth);
    return Results.Ok();
}).DisableAntiforgery();

// A chat visitor asks for a browser notification when Martin answers (wwwroot/hjem.js visitorPush).
app.MapPost("/api/push/visitor", async (VisitorSubRequest req, ChatUseCases chat) =>
    await chat.SetVisitorPushAsync(req.VisitorKey, req.Endpoint, req.P256dh, req.Auth) ? Results.Ok() : Results.BadRequest())
    .DisableAntiforgery();

app.MapPost("/api/push/test", async (HttpContext ctx, PushUseCases push) =>
    ctx.User.IsOwner() ? Results.Ok(await push.SendToOwnerAsync("Martin Hvidberg", "Beskeder virker ✅ – her kommer spørgsmål fra din side.", "/admin")) : Results.Unauthorized())
    .DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

// Behind the Cloudflare tunnel the visitor's IP is in CF-Connecting-IP.
static string ClientIp(HttpContext ctx) =>
    ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "";

static bool SameText(string a, string b) =>
    CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

static string LoginPage(bool owner, bool err) => $$"""
<!DOCTYPE html>
<html lang="da" data-family="rede"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{(owner ? "Log ind" : "Familie")}} · Martin Hvidberg</title>
<link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css"><link rel="stylesheet" href="/app.css"></head>
<body><main class="k-main"><form class="k-card k-pin" method="post" action="/login">
<input type="hidden" name="who" value="{{(owner ? "ejer" : "familie")}}">
<div class="k-card-title">{{(owner ? "🔑 Martins egen adgang" : "🔒 Familiens billeder")}}</div>
<p class="k-card-sub">{{(owner ? "Skriv din PIN for at skrive opslag og svare på beskeder." : "Skriv familiens kodeord for at se familiens billeder og videoer. Kender du det ikke, så spørg Martin – skriv til ham på forsiden.")}}</p>
{{(err ? "<p class=\"k-error\">Forkert – prøv igen.</p>" : "")}}
<input class="k-input" type="password" name="code" autocomplete="current-password" autofocus required aria-label="Kode">
<button class="k-btn k-btn-primary k-btn-block k-mt" type="submit">Åbn</button>
<p class="k-help"><a href="/">← Tilbage til forsiden</a></p>
</form></main></body></html>
""";

public sealed record SubRequest(string Endpoint, string P256dh, string Auth);
public sealed record VisitorSubRequest(string VisitorKey, string Endpoint, string P256dh, string Auth);
