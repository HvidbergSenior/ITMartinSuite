using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITMartin.Shared.UI.Kolibri;
using ITMartinFaellesskab.Application;
using ITMartinFaellesskab.Domain;
using ITMartinFaellesskab.Infrastructure;
using Microsoft.AspNetCore.DataProtection;

// Fællesskab (Kolibri Fællesskab) - 2026-10-29, user: "i do not want to make a traditionel webshop ... customer oriented
// focus ... Interested in books -> make a book club app", "I want communities with books, anders and blade. etc. For nerds",
// + Magic, PC games, board games, "remember the moviebox? ... buy 5 DVDs", music. Snak was the template.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Fællesskab";
    k.KolibriName = "Kolibri Fællesskab";
    k.Family = "kommunikere";
    k.Tagline = "Fællesskaber for nørder – bøger, Anders And, blade, Magic, spil, film og musik.";
    k.Version = "2026.10.29";
    k.ThemeColor = "#f4f6f5";
    k.About =
    [
        "Fællesskab samler folk, der interesserer sig for det samme: en læseklub for bøger, samlere af Anders And og blade, Magic-spillere, brætspil, PC-spil, videoaften og musik.",
        "Hvert fællesskab har noget, vi kigger på lige nu, en dato hvor vi mødes, tips til mere af det samme og en chat. Der er ingen reklamer, og intet bliver solgt eller delt.",
        "Fællesskab hører til Bogshoppen, som er en del af ITMartin.",
    ];
    k.HowTo =
    [
        "Åbn faellesskab.itmartin.dk og tryk på det fællesskab, du interesserer dig for. Alle kan se, hvad vi kigger på lige nu, og tipsene.",
        "Vil du skrive i chatten, så log ind med dit kundenummer (de 8 cifre fra mailen). Har du ikke et, så skriv dig op gratis på bogshoppen.dk.",
        "Tryk \"Jeg er med\" for at være med i et fællesskab. Tryk igen, hvis du vil ud.",
        "Læg Fællesskab på telefonen, så den åbner som en app.",
    ];
    // The page is a static index.html (like Snak), so /om and /hjaelp come as plain HTML.
    k.StaticPages = true;
    k.HealthChecks = sp => Task.FromResult<IReadOnlyList<KolibriHealthItem>>(
    [
        new("data", Directory.Exists(sp.GetRequiredService<IConfiguration>()["Faellesskab:DataDir"] ?? "/data")),
    ]);
});

// DDD/Clean: the endpoints below only call Application (Communities, Tips, Access).
var dataDir = builder.Configuration["Faellesskab:DataDir"] ?? "/data";
var adminCode = builder.Configuration["Faellesskab:AdminCode"] ?? "";
builder.Services.AddFaellesskabApplication(adminCode);
builder.Services.AddFaellesskabInfrastructure(dataDir, builder.Configuration["Kunder:Url"], builder.Configuration["Kunder:ApiKey"]);
// The kundenummer sits data-protected in the cookie, so nobody can type another person's number into it; keys in the data folder.
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys"))).SetApplicationName("faellesskab");

var app = builder.Build();
app.MapKolibri();
app.UseDefaultFiles();
// The page must always be fetched fresh (css/js carry ?v= in index.html, so they can be cached).
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => { if (ctx.File.Name.EndsWith(".html")) ctx.Context.Response.Headers.CacheControl = "no-cache"; }
});

var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var protector = app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("faellesskab.kunde.v1");
var adminToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("admin:" + adminCode)));
const string CustomerCookie = "fk", AdminCookie = "fk_admin";

string? Customer(HttpContext c)
{
    if (c.Request.Cookies[CustomerCookie] is not { Length: > 0 } raw) return null;
    try { return protector.Unprotect(raw); } catch (CryptographicException) { return null; }
}
bool IsAdmin(HttpContext c) => adminCode.Length >= Access.MinAdminCode && c.Request.Cookies[AdminCookie] == adminToken;

// Behind the Cloudflare tunnel every request comes from cloudflared, so the visitor is in this header.
static string ClientIp(HttpContext c) =>
    c.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? c.Connection.RemoteIpAddress?.ToString() ?? "?";

object Shape(CommunityView v) => new
{
    slug = v.Community.Slug, title = v.Community.Title, line = v.Community.Line, activity = v.Community.Activity,
    nowLabel = v.Community.NowLabel, now = v.Now is { } n ? new { n.Title, n.Note, n.When } : null, members = v.Members, joined = v.Joined,
    hasTips = v.Community.Tips != TipKind.None,
};

// ---- login ----

app.MapPost("/api/login", async (HttpContext c, LoginBody b, Access access) =>
{
    var (result, code, error) = await access.LoginAsync(b.Kode, b.Navn, ClientIp(c), c.RequestAborted);
    var opts = new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(400) };
    switch (result)
    {
        case LoginResult.Admin:
            c.Response.Cookies.Append(AdminCookie, adminToken, opts);
            return Results.Ok(new { admin = true });
        case LoginResult.Customer:
            c.Response.Cookies.Append(CustomerCookie, protector.Protect(code), opts);
            return Results.Ok(new { admin = IsAdmin(c) });
        case LoginResult.Locked:
            return Results.Json(new { fejl = error }, statusCode: 429);
        case LoginResult.Unreachable:
            return Results.Json(new { fejl = error }, statusCode: 503);
        default:
            return Results.Json(new { fejl = error }, statusCode: 401);
    }
});

app.MapPost("/api/logout", (HttpContext c) =>
{
    c.Response.Cookies.Delete(CustomerCookie);
    c.Response.Cookies.Delete(AdminCookie);
    return Results.Ok();
});

app.MapGet("/api/me", (HttpContext c, Communities communities) =>
{
    var me = communities.Me(Customer(c));
    return Results.Ok(new { loggedIn = me is not null, name = me?.Name ?? "", admin = IsAdmin(c) });
});

app.MapPost("/api/navn", (HttpContext c, NameBody b, Communities communities) =>
    communities.SetName(Customer(c), b.Navn) is { } m ? Results.Ok(new { name = m.Name }) : Results.BadRequest(new { fejl = "Skriv dit navn." }));

// ---- communities: everyone sees them, "right now" and the tips ----

app.MapGet("/api/faellesskaber", (HttpContext c, Communities communities) =>
    Results.Json(communities.List(Customer(c)).Select(Shape), web));

app.MapGet("/api/faellesskaber/{slug}", (HttpContext c, string slug, Communities communities) =>
    communities.Get(slug, Customer(c)) is { } v ? Results.Json(Shape(v), web) : Results.NotFound());

app.MapGet("/api/faellesskaber/{slug}/tips", async (HttpContext c, string slug, Tips tips) =>
    Results.Json(await tips.ForAsync(slug, c.RequestAborted), web));

app.MapPost("/api/faellesskaber/{slug}/med", (HttpContext c, string slug, Communities communities) =>
    communities.ToggleJoin(Customer(c), slug) is { } m ? Results.Ok(new { joined = m.Slugs.Contains(slug) }) : Results.Unauthorized());

// ---- the chat: only logged-in customers (names are not shown to everyone) ----

app.MapGet("/api/faellesskaber/{slug}/beskeder", (HttpContext c, string slug, long? efter, Communities communities) =>
    communities.Me(Customer(c)) is null && !IsAdmin(c) ? Results.Unauthorized() : Results.Json(communities.MessagesAfter(slug, efter ?? 0), web));

app.MapPost("/api/faellesskaber/{slug}/beskeder", (HttpContext c, string slug, MessageBody b, Communities communities) =>
{
    if (communities.Me(Customer(c)) is not { } me) return Results.Unauthorized();
    if (me.Name.Length == 0) return Results.BadRequest(new { fejl = "Skriv dit navn først – det ser de andre ved dine beskeder." });
    return communities.Write(me.Code, slug, b.Tekst) is { } m ? Results.Json(m, web) : Results.BadRequest(new { fejl = "Skriv en besked." });
});

// ---- right now + when we meet: Martin (admin code) ----

app.MapPut("/api/faellesskaber/{slug}/nu", (HttpContext c, string slug, NowBody b, Communities communities) =>
{
    if (!IsAdmin(c)) return Results.StatusCode(403);
    return communities.SetNow(slug, b.Titel, b.Note, b.Hvornaar) is { } n ? Results.Json(n, web) : Results.BadRequest(new { fejl = "Skriv en titel." });
});

app.MapDelete("/api/faellesskaber/{slug}/nu", (HttpContext c, string slug, Communities communities) =>
    !IsAdmin(c) ? Results.StatusCode(403) : communities.ClearNow(slug) ? Results.Ok() : Results.NotFound());

app.Run();

public sealed record LoginBody(string? Kode, string? Navn);
public sealed record NameBody(string? Navn);
public sealed record MessageBody(string? Tekst);
public sealed record NowBody(string? Titel, string? Note, string? Hvornaar);

public partial class Program;
