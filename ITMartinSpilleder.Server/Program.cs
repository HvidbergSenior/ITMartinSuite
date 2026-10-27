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
    k.Name = "Game Master";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Help at the table for the game master and the players – secret messages, players and hit points.";
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
    k.About =
    [
        "Game Master helps you at the table when you play role-playing games like Dungeons & Dragons, Werewolf or a murder mystery.",
        "Everyone can see the players: name, character, race, class, hit points and spells. When a player loses hit points, everyone sees it at once.",
        "The game master can send a secret message to one player, like \"You see something glinting to your right\". Only that player sees it.",
        "Spells from the D&D 5e SRD 5.1 by Wizards of the Coast, used under CC-BY-4.0 (creativecommons.org/licenses/by/4.0). No ads.",
    ];
    k.HowTo =
    [
        "The game master opens spilleder.itmartin.dk/master, starts a game and gets a game code. The players open spilleder.itmartin.dk/player and enter the code and their name.",
        "Under Players each player fills in their card. Press − and + to change hit points.",
        "Under Messages the game master chooses who the message is for. Roll the dice in the same place.",
        "Made your character on character.itmartin.dk? Press Edit card, then Import from Character, and write the 6-letter figure code. Hit points, gear and notes go back to Character.",
    ];
    k.StaticPages = true;
    k.HealthChecks = sp => Task.FromResult<IReadOnlyList<KolibriHealthItem>>(
    [
        new("data", Directory.Exists(sp.GetRequiredService<IConfiguration>()["Spilleder:DataDir"] ?? "/data")),
    ]);
});

var dataDir = builder.Configuration["Spilleder:DataDir"] ?? "/data";
builder.Services.AddSpillederInfrastructure(dataDir, builder.Configuration);
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

// Two entrances to the same app, like R6 (user 2026-10-27): /master for the game master, /player for the players
// (the Danish /leder and /spiller from the first version keep working).
// Same page; app.js shows the right start box from the address.
foreach (var entrance in new[] { "/master", "/player", "/leder", "/spiller" })
    app.MapGet(entrance, (HttpContext c) =>
    {
        c.Response.Headers.CacheControl = "no-cache";
        return Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html; charset=utf-8");
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

IResult Fejl(JoinError e, string message = "") => e switch
{
    JoinError.Customer => Results.BadRequest(new { fejl = message }),
    JoinError.NotInGame => Results.BadRequest(new { fejl = "You are not in a game yet. Ask the game master to add your customer number, or join with the game code." }),
    JoinError.AlreadyInGame => Results.BadRequest(new { fejl = "That customer is already in this game." }),
    JoinError.Locked => Results.Json(new { fejl = "Too many wrong tries. Wait 15 minutes and try again." }, statusCode: 429),
    JoinError.WrongCode => Results.Json(new { fejl = "That code is not right. Ask the game master." }, statusCode: 401),
    JoinError.Full => Results.BadRequest(new { fejl = "There is no room for more players in this game." }),
    _ => Results.BadRequest(new { fejl = "Enter your name." }),
};

// A game master starts a game with their kundenummer (the old shared start code Spilleder:Code still works if set).
app.MapPost("/api/nyt-spil", async (HttpContext c, NewGameBody b, Tables t) =>
{
    if (GameRules.CleanCustomerNo(b.Kode).Length == 8)
    {
        var r = await t.StartAsync(b.Kode, b.Titel, b.Navn, ClientIp(c), c.RequestAborted);
        return r.Joined is null ? Fejl(r.Error, r.Message) : SignIn(c, r.Joined);
    }
    var (j, e) = t.Create(b.Kode, b.Titel, b.Navn, ClientIp(c));
    return j is null ? Fejl(e) : SignIn(c, j);
});

// One field: a kundenummer (8 digits), the game code (guests), a personal code or the master code.
app.MapPost("/api/deltag", async (HttpContext c, JoinBody b, Tables t) =>
{
    if (GameRules.CleanCustomerNo(b.Kode).Length == 8)
    {
        var r = await t.LoginCustomerAsync(b.Kode, ClientIp(c), c.RequestAborted);
        return r.Joined is null ? Fejl(r.Error, r.Message) : SignIn(c, r.Joined);
    }
    var (j, e) = t.Join(b.Kode, b.Navn, ClientIp(c));
    return j is null ? Fejl(e) : SignIn(c, j);
});

// The game master adds a customer as a player by their kundenummer.
app.MapPost("/api/spillere/kunde", async (HttpContext c, CustomerBody b, Tables t) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    var (p, e, m) = await t.AddCustomerAsync(w.Game, w.Viewer, b.Kundenummer, c.RequestAborted);
    return p is not null ? Results.Json(p, web)
        : e == JoinError.WrongCode ? Results.BadRequest(new { fejl = "Write the customer number – 8 digits." }) : Fejl(e, m);
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
    if (c.Request.ContentLength > GameRules.MaxPictureBytes) return Results.BadRequest(new { fejl = "The picture is too big." });
    using var ms = new MemoryStream();
    await c.Request.Body.CopyToAsync(ms, c.RequestAborted);
    var p = t.SetPicture(w.Game, w.Viewer, id, ms.ToArray());
    return p is null ? Results.BadRequest(new { fejl = "The picture could not be saved. Try another one." }) : Results.Json(p, web);
});

// A ready figure from Character (character.itmartin.dk) fills the card - by its 6-letter figure code.
app.MapPost("/api/spillere/{id}/import", async (HttpContext c, string id, ImportBody b, Tables t) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    var (p, error) = await t.ImportAsync(w.Game, w.Viewer, id, b.Kode, c.RequestAborted);
    return p is null ? Results.BadRequest(new { fejl = error }) : Results.Json(p, web);
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
    return n is null ? Results.BadRequest(new { fejl = "Write a message." }) : Results.Json(n, web);
});

app.MapPost("/api/terning", (HttpContext c, DiceBody b, Tables t) =>
{
    if (Who(c, t) is not { } w) return Results.Unauthorized();
    var n = t.Roll(w.Game, w.Viewer, b.Terning, b.Hemmelig, b.Hvorfor);
    return n is null ? Results.BadRequest(new { fejl = "Write the dice like d20, 2d6 or 1d8+2." }) : Results.Json(n, web);
});

// ---- spells from the free D&D 5e SRD ----

app.MapGet("/api/besvaergelser", async (HttpContext c, string? q, Tables t, ISpellBook book) =>
{
    if (Who(c, t) is null) return Results.Unauthorized();
    try { return Results.Json(await book.SearchAsync(q ?? "", c.RequestAborted), web); }
    catch (Exception) { return Results.Json(new { fejl = "The spell look-up is not answering right now. Write what it does yourself." }, statusCode: 502); }
});

app.MapGet("/api/besvaergelser/{index}", async (HttpContext c, string index, Tables t, ISpellBook book) =>
{
    if (Who(c, t) is null) return Results.Unauthorized();
    try { return await book.GetAsync(index, c.RequestAborted) is { } s ? Results.Json(s, web) : Results.NotFound(); }
    catch (Exception) { return Results.Json(new { fejl = "The spell look-up is not answering right now. Write what it does yourself." }, statusCode: 502); }
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
public sealed record ImportBody(string? Kode);
public sealed record CustomerBody(string? Kundenummer);
public sealed record HpBody(int Aendring);
public sealed record NoteBody(List<string>? Til, string? Tekst);
public sealed record DiceBody(string? Terning, bool Hemmelig, string? Hvorfor);
public sealed record PushBody(string? Endpoint, string? P256dh, string? Auth);

public partial class Program;
