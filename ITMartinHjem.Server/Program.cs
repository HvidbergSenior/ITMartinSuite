using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ITMartin.Shared.UI.Kolibri;
using ITMartinHjem.Server;
using ITMartinHjem.Server.Data;
using ITMartinHjem.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddCascadingAuthenticationState();

var dataDir = builder.Configuration["Hjem:DataDir"] ?? "/app/data";
Directory.CreateDirectory(dataDir);
builder.Services.AddDbContextFactory<HjemDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "hjem.db")}"));
builder.Services.AddSingleton<MediaStore>();
builder.Services.AddSingleton<PushService>();
builder.Services.AddSingleton<ChatService>();

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
// redirects (/pilot -> /pilot/) and absolute links stay on https.
var fwd = new ForwardedHeadersOptions { ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto };
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);
app.MapKolibri();

await using (var db = await app.Services.GetRequiredService<IDbContextFactory<HjemDb>>().CreateDbContextAsync())
{
    await db.Database.EnsureCreatedAsync();
    await Schema.EnsureColumnsAsync(db);
    if (!await db.Settings.AnyAsync())
    {
        db.Settings.Add(new Settings
        {
            Id = 1,
            Intro = "Er du interesseret, viser og deler jeg gerne, hvad jeg laver, og mine tanker om produkterne.",
            NowText = "Jeg bygger små apps, der hjælper folk med at gøre det rigtige over for store selskaber. Lige nu: MinElpris – så du kan se, om du betaler for meget for strømmen.",
        });
        await db.SaveChangesAsync();
    }
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

// Only this site and itmartin.dk may put these pages in a frame (the /chat bubble there).
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self' https://itmartin.dk https://www.itmartin.dk";
    await next();
});
app.UseDefaultFiles();   // /pilot/ and /rejsedemo/ -> their index.html
app.UseStaticFiles(new StaticFileOptions
{
    // widget.js is loaded by itmartin.dk: allow it, and keep the cache short so changes show up.
    OnPrepareResponse = c =>
    {
        if (c.File.Name == "widget.js") c.Context.Response.Headers.CacheControl = "public, max-age=300";
    }
});
// Routing AFTER static files: the /{*Slug} page route would otherwise answer app.css, hjem.js …
app.UseRouting();
app.UseCors();

// ── Live "Lige nu" for itmartin.dk (wwwroot/widget.js reads this) ───────────────────
app.MapGet("/api/nu", (IDbContextFactory<HjemDb> dbf) =>
{
    using var db = dbf.CreateDbContext();
    var s = db.Settings.AsNoTracking().First();
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
        updated = s.NowUpdated.ToLocalTime().ToString("d. MMMM yyyy", new System.Globalization.CultureInfo("da-DK")),
        available = s.Available,
        player,
        link = player is null && s.NowLink.Length > 0 ? s.NowLink : null,
        media,
    });
}).RequireCors("itmartin");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Login: one small form, two kinds (familie = password, ejer = PIN) ───────────────
app.MapGet("/login", (string? who, string? err) => Results.Content(LoginPage(who == "ejer", err is not null), "text/html"));

app.MapPost("/login", async (HttpContext ctx, IConfiguration cfg) =>
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
    return Results.Redirect(owner ? "/admin" : "/");
}).DisableAntiforgery();

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// ── "Lige nu" picture/film (always public). The file name changes on every upload, so it caches well.
app.MapGet("/nu/{name}", (string name, HttpContext ctx, IDbContextFactory<HjemDb> dbf, MediaStore store) =>
{
    using var db = dbf.CreateDbContext();
    var s = db.Settings.AsNoTracking().First();
    if (name != s.NowMediaName && name != s.NowMediaThumb) return Results.NotFound();
    var path = store.PathFor(name);
    if (!File.Exists(path)) return Results.NotFound();
    ctx.Response.Headers.CacheControl = "public, max-age=604800";
    return Results.File(path, name == s.NowMediaThumb ? "image/jpeg" : s.NowMediaType, enableRangeProcessing: true);
});

app.MapGet("/pilot", () => Results.Redirect("/pilot/", permanent: true));
app.MapGet("/rejsedemo", () => Results.Redirect("/rejsedemo/", permanent: true));

// ── The site menu for hand-made pages in wwwroot (pilot): wwwroot/nav.js draws it from this.
app.MapGet("/api/menu", async (IDbContextFactory<HjemDb> dbf) =>
{
    await using var db = await dbf.CreateDbContextAsync();
    var items = await db.Pages.AsNoTracking().Where(p => p.InMenu).OrderBy(p => p.Sort).ThenBy(p => p.Title)
        .Select(p => new { title = p.Title, href = "/" + p.Slug }).ToListAsync();
    items.Insert(0, new { title = "Forside", href = "/" });
    items.Add(new { title = "🐦 Bliv pilot", href = "/pilot/" });
    return Results.Json(items);
});

// ── The pilot form (wwwroot/pilot/index.html posts to send.php, as it did on one.com).
// Instead of a mail it becomes a conversation in Svar, with a push to Martin's phone.
app.MapPost("/pilot/send.php", async (HttpContext ctx, ChatService chat) =>
{
    var form = await ctx.Request.ReadFormAsync();
    string F(string k) => form[k].ToString().Trim();
    if (F("website").Length > 0) return Results.Redirect("/pilot/");            // spam trap
    var (ydelse, navn, tlf, by, besked) = (F("ydelse"), F("navn"), F("telefon"), F("by"), F("besked"));
    if (navn.Length == 0 || tlf.Length == 0 || ydelse.Length == 0) return Results.Redirect("/pilot/#form");
    await chat.VisitorWritesAsync("pilot-" + Guid.NewGuid().ToString("N"), $"{navn} (pilot)",
        $"🐦 Pilot-henvendelse: {ydelse}\nTelefon: {tlf}\nBy: {by}\n\n{besked}");
    Func<string?, string?> e = System.Net.WebUtility.HtmlEncode;
    return Results.Content($$"""
        <!doctype html><html lang="da"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Tak</title>
        <style>body{font:18px/1.5 system-ui,sans-serif;max-width:560px;margin:60px auto;padding:0 16px;color:#1d2a25}a{color:#1f7a5c}</style></head>
        <body><h1>Tak, {{e(navn)}}!</h1>
        <p>Jeg har fået din besked om <b>{{e(ydelse)}}</b> og ringer til dig på {{e(tlf)}} inden for to dage.</p>
        <p><a href="/pilot/">Tilbage</a> · <a href="/">Til forsiden</a></p></body></html>
        """, "text/html");
}).DisableAntiforgery();

// ── Pictures on the pages (Om mig …): only files that a page actually uses.
app.MapGet("/f/{name}", async (string name, HttpContext ctx, IDbContextFactory<HjemDb> dbf, MediaStore store) =>
{
    name = Path.GetFileName(name);
    await using var db = await dbf.CreateDbContextAsync();
    if (!await db.Pages.AnyAsync(p => p.Body.Contains("/f/" + name))) return Results.NotFound();
    var path = store.PathFor(name);
    if (!File.Exists(path)) return Results.NotFound();
    ctx.Response.Headers.CacheControl = "public, max-age=604800";
    return Results.File(path, name.EndsWith(".png") ? "image/png" : "image/jpeg");
});

// ── Media: family files only with the family cookie ─────────────────────────────────
app.MapGet("/m/{id:int}/{size}", async (int id, string size, HttpContext ctx, IDbContextFactory<HjemDb> dbf, MediaStore store) =>
{
    await using var db = await dbf.CreateDbContextAsync();
    var m = await db.Media.Include(x => x.Post).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (m is null || (m.Post.Visibility == Visibility.Familie && !ctx.User.IsFamily())) return Results.NotFound();
    var name = size == "t" && m.ThumbName is not null ? m.ThumbName : m.StoredName;
    var path = store.PathFor(name);
    if (!File.Exists(path)) return Results.NotFound();
    ctx.Response.Headers.CacheControl = m.Post.Visibility == Visibility.Familie ? "private, max-age=86400" : "public, max-age=604800";
    return Results.File(path, m.ContentType, size == "d" ? m.FileName : null, enableRangeProcessing: true);
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
app.MapGet("/api/push/public-key", (PushService push) => Results.Text(push.PublicKey));

app.MapPost("/api/push/subscribe", async (SubRequest req, HttpContext ctx, IDbContextFactory<HjemDb> dbf) =>
{
    if (!ctx.User.IsOwner()) return Results.Unauthorized();
    await using var db = await dbf.CreateDbContextAsync();
    if (!await db.PushSubs.AnyAsync(s => s.Endpoint == req.Endpoint))
        db.PushSubs.Add(new PushSub { Endpoint = req.Endpoint, P256dh = req.P256dh, Auth = req.Auth });
    await db.SaveChangesAsync();
    return Results.Ok();
}).DisableAntiforgery();

app.MapPost("/api/push/test", async (HttpContext ctx, PushService push) =>
    ctx.User.IsOwner() ? Results.Ok(await push.SendToOwnerAsync("Martin Hvidberg", "Beskeder virker ✅ – her kommer spørgsmål fra din side.", "/admin")) : Results.Unauthorized())
    .DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

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
