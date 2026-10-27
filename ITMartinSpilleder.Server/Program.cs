using System.Text.Json;
using ITMartin.Shared.UI.Kolibri;
using ITMartinSpilleder.Application;
using ITMartinSpilleder.Domain;
using ITMartinSpilleder.Infrastructure;

// Spilleder (Kolibri Kommunikere) - 2026-10-27, for Julius who is trying to be game master. User: "an app that can help you
// with the adventure, both as dungeon master and player ... small messages like 'You see something interesting to the right'
// - the things you need to tell one player and not the group"; "do not have to be D&D specific"; "first of all an overview
// of players, names, races etc - so everybody can see all and do not forget it"; "what do your spells do?"; "like my
// StarRealms app - when hitpoints drop the other guys can see it".
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Spilleder";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Hjælp til spillederen og spillerne ved bordet – hemmelige beskeder, spillere og liv.";
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
    k.About =
    [
        "Spilleder hjælper jer ved bordet, når I spiller rollespil som Dungeons & Dragons, Varulv eller et mordmysterium.",
        "Alle kan se spillerne: navn, figur, race, klasse, liv og besværgelser. Når en spiller mister liv, ser alle det med det samme.",
        "Spillederen kan sende en hemmelig besked til én spiller, fx \"Du ser noget glimte til højre\". Kun den spiller ser den.",
        "Besværgelser fra D&D 5e SRD 5.1 af Wizards of the Coast, brugt under CC-BY-4.0 (creativecommons.org/licenses/by/4.0). Ingen reklamer.",
    ];
    k.HowTo =
    [
        "Spillederen starter et spil og får en spilkode. Spillerne åbner spilleder.itmartin.dk, skriver koden og deres navn.",
        "Under Spillere udfylder hver spiller sit kort. Tryk − og + for at ændre liv.",
        "Under Beskeder vælger spillederen, hvem beskeden er til. Slå med terningerne samme sted.",
    ];
    k.StaticPages = true;
    k.HealthChecks = sp => Task.FromResult<IReadOnlyList<KolibriHealthItem>>(
    [
        new("data", Directory.Exists(sp.GetRequiredService<IConfiguration>()["Spilleder:DataDir"] ?? "/data")),
    ]);
});

var dataDir = builder.Configuration["Spilleder:DataDir"] ?? "/data";
builder.Services.AddSpillederInfrastructure(dataDir);
builder.Services.AddSpillederApplication(builder.Configuration["Spilleder:Code"] ?? "");

var app = builder.Build();
app.MapKolibri();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        if (name.EndsWith(".html") || name == "spil-sw.js")
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});

var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
const string Cookie = "spil";

// Behind the Cloudflare tunnel every request comes from cloudflared, so the visitor is in this header.
static string ClientIp(HttpContext c) =>
    c.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? c.Connection.RemoteIpAddress?.ToString() ?? "?";

// The cookie is "<game id>.<token>"; the token is checked against the hash in the game.
(string Game, string Viewer)? Who(HttpContext c, Tables t)
{
    var v = c.Request.Cookies[Cookie];
    var dot = v?.IndexOf('.') ?? -1;
    if (dot <= 0) return null;
    var game = v![..dot];
    var viewer = t.WhoIs(game, v[(dot + 1)..]);
    return viewer is null ? null : (game, viewer);
}

IResult SignIn(HttpContext c, Joined j)
{
    c.Response.Cookies.Append(Cookie, j.GameId + "." + j.Token,
        new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(400) });
    return Results.Ok(new { leder = j.Viewer == Viewer.Leader });
}

IResult Fejl(JoinError e) => e switch
{
    JoinError.Locked => Results.Json(new { fejl = "For mange forkerte forsøg. Vent et kvarter og prøv igen." }, statusCode: 429),
    JoinError.WrongCode => Results.Json(new { fejl = "Koden passer ikke. Spørg spillederen." }, statusCode: 401),
    JoinError.Full => Results.BadRequest(new { fejl = "Der er ikke plads til flere spillere i dette spil." }),
    _ => Results.BadRequest(new { fejl = "Skriv dit navn." }),
};

app.MapPost("/api/nyt-spil", (HttpContext c, NewGameBody b, Tables t) =>
{
    var (j, e) = t.Create(b.Kode, b.Titel, b.Navn, ClientIp(c));
    return j is null ? Fejl(e) : SignIn(c, j);
});

app.MapPost("/api/deltag", (HttpContext c, JoinBody b, Tables t) =>
{
    var (j, e) = t.Join(b.Kode, b.Navn, ClientIp(c));
    return j is null ? Fejl(e) : SignIn(c, j);
});

app.MapPost("/api/forlad", (HttpContext c) => { c.Response.Cookies.Delete(Cookie); return Results.Ok(); });

app.MapGet("/api/spil", (HttpContext c, Tables t, IPushSender push) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    var v = t.View(w.Game, w.Viewer);
    return v is null ? Results.Unauthorized() : Results.Json(new { spil = v, pushKey = push.PublicKey }, web);
});

app.MapPut("/api/spil", (HttpContext c, RenameBody b, Tables t) =>
    Who(c, t) is { } w && t.Rename(w.Game, w.Viewer, b.Titel, b.Navn) ? Results.Ok() : Results.StatusCode(403));

// ---- the player cards: everyone sees all; a player edits their own, the leader every card ----

app.MapPut("/api/spillere/{id}", (HttpContext c, string id, CardBody b, Tables t) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    var p = t.EditCard(w.Game, w.Viewer, id, b.Navn, b.Figur, b.Race, b.Klasse, b.Liv, b.MaxLiv, b.Noter, b.Udstyr,
        b.Besvaergelser?.Select(s => new Spell(s.Navn ?? "", s.Tekst ?? "")));
    return p is null ? Results.StatusCode(403) : Results.Json(p, web);
});

app.MapPost("/api/spillere/{id}/liv", (HttpContext c, string id, HpBody b, Tables t) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    var p = t.ChangeHp(w.Game, w.Viewer, id, b.Aendring);
    return p is null ? Results.StatusCode(403) : Results.Json(p, web);
});

// The character picture: the phone shrinks it to a small JPEG first, so it comes as the raw body.
app.MapPost("/api/spillere/{id}/billede", async (HttpContext c, string id, Tables t) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    if (c.Request.ContentLength > GameRules.MaxPictureBytes) return Results.BadRequest(new { fejl = "Billedet er for stort." });
    using var ms = new MemoryStream();
    await c.Request.Body.CopyToAsync(ms, c.RequestAborted);
    var p = t.SetPicture(w.Game, w.Viewer, id, ms.ToArray());
    return p is null ? Results.BadRequest(new { fejl = "Billedet kunne ikke gemmes. Prøv et andet." }) : Results.Json(p, web);
});

app.MapGet("/api/billede/{id}", (HttpContext c, string id, Tables t) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    c.Response.Headers.CacheControl = "private, max-age=31536000";
    return t.Picture(w.Game, id) is { } b ? Results.File(b, "image/jpeg") : Results.NotFound();
});

// The leader opens a saved sheet for changes, or locks it again.
app.MapPost("/api/spillere/{id}/laas", (HttpContext c, string id, LockBody b, Tables t) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    var p = t.SetLocked(w.Game, w.Viewer, id, b.Laast);
    return p is null ? Results.StatusCode(403) : Results.Json(p, web);
});

app.MapDelete("/api/spillere/{id}", (HttpContext c, string id, Tables t) =>
    Who(c, t) is { } w && t.RemovePlayer(w.Game, w.Viewer, id) ? Results.Ok() : Results.StatusCode(403));

// ---- secret messages and dice ----

app.MapPost("/api/beskeder", (HttpContext c, NoteBody b, Tables t) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    var n = t.Write(w.Game, w.Viewer, b.Til, b.Tekst);
    return n is null ? Results.BadRequest(new { fejl = "Skriv en besked." }) : Results.Json(n, web);
});

app.MapPost("/api/terning", (HttpContext c, DiceBody b, Tables t) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    var n = t.Roll(w.Game, w.Viewer, b.Terning, b.Hemmelig, b.Hvorfor);
    return n is null ? Results.BadRequest(new { fejl = "Skriv terningen som fx d20, 2d6 eller 1d8+2." }) : Results.Json(n, web);
});

// ---- spells from the free D&D 5e SRD ----

app.MapGet("/api/besvaergelser", async (HttpContext c, string? q, Tables t, ISpellBook book) =>
{
    if (Who(c, t) is null) return Results.Unauthorized();
    try { return Results.Json(await book.SearchAsync(q ?? "", c.RequestAborted), web); }
    catch (Exception) { return Results.Json(new { fejl = "Opslaget svarer ikke lige nu. Skriv selv, hvad den gør." }, statusCode: 502); }
});

app.MapGet("/api/besvaergelser/{index}", async (HttpContext c, string index, Tables t, ISpellBook book) =>
{
    if (Who(c, t) is null) return Results.Unauthorized();
    try { return await book.GetAsync(index, c.RequestAborted) is { } s ? Results.Json(s, web) : Results.NotFound(); }
    catch (Exception) { return Results.Json(new { fejl = "Opslaget svarer ikke lige nu. Skriv selv, hvad den gør." }, statusCode: 502); }
});

// ---- live updates + push ----

app.MapGet("/api/live", async (HttpContext c, Tables t, ILiveFeed live) =>
{
    if (Who(c, t) is not { } w) { c.Response.StatusCode = 401; return; }
    c.Response.Headers.ContentType = "text/event-stream";
    c.Response.Headers.CacheControl = "no-cache";
    c.Response.Headers["X-Accel-Buffering"] = "no";
    var (id, reader) = live.Join(w.Game, w.Viewer);
    try
    {
        await c.Response.WriteAsync("event: hej\ndata: {}\n\n", c.RequestAborted);
        await c.Response.Body.FlushAsync(c.RequestAborted);
        while (!c.RequestAborted.IsCancellationRequested)
        {
            // A comment line every 20 s keeps Cloudflare from closing a quiet line (~100 s limit).
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted);
            wait.CancelAfter(TimeSpan.FromSeconds(20));
            string line;
            try { line = await reader.ReadAsync(wait.Token); }
            catch (OperationCanceledException) when (!c.RequestAborted.IsCancellationRequested) { line = ": ping\n\n"; }
            await c.Response.WriteAsync(line, c.RequestAborted);
            await c.Response.Body.FlushAsync(c.RequestAborted);
        }
    }
    catch (OperationCanceledException) { }
    finally { live.Leave(id); }
});

app.MapPost("/api/push", (HttpContext c, PushBody b, Tables t, IPushSender push) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(b.Endpoint) || !b.Endpoint.StartsWith("https://") ||
        string.IsNullOrWhiteSpace(b.P256dh) || string.IsNullOrWhiteSpace(b.Auth))
        return Results.BadRequest();
    push.Subscribe(w.Game, w.Viewer, b.Endpoint, b.P256dh, b.Auth);
    return Results.Ok();
});

app.MapPost("/api/push/fra", (HttpContext c, PushBody b, Tables t, IPushSender push) =>
{
    if (Who(c, t) is null) return Results.Unauthorized();
    if (!string.IsNullOrWhiteSpace(b.Endpoint)) push.Unsubscribe(b.Endpoint);
    return Results.Ok();
});

app.Run();

public sealed record NewGameBody(string? Kode, string? Titel, string? Navn);
public sealed record JoinBody(string? Kode, string? Navn);
public sealed record RenameBody(string? Titel, string? Navn);
public sealed record SpellBody(string? Navn, string? Tekst);
public sealed record CardBody(string? Navn, string? Figur, string? Race, string? Klasse, int Liv, int MaxLiv, string? Noter, string? Udstyr, List<SpellBody>? Besvaergelser);
public sealed record LockBody(bool Laast);
public sealed record HpBody(int Aendring);
public sealed record NoteBody(List<string>? Til, string? Tekst);
public sealed record DiceBody(string? Terning, bool Hemmelig, string? Hvorfor);
public sealed record PushBody(string? Endpoint, string? P256dh, string? Auth);

public partial class Program;
