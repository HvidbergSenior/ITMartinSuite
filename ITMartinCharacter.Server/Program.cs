using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITMartin.Shared.UI.Kolibri;
using ITMartinCharacter.Application;
using ITMartinCharacter.Domain;
using ITMartinCharacter.Infrastructure;

// Character (Kolibri Kommunikere) - 2026-10-27. User: "there should be an app that focuses on character generation ... and data
// from that should be used during adventure"; "call it character.itmartin.dk. It should be a cooperation between game master
// and player. So game master should be able to help remote as well". Game Master (spilleder.itmartin.dk) imports the figure.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Character";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Make your role-playing character step by step – with your game master, at the table or from far away.";
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
    k.About =
    [
        "Character helps you make a figure for a role-playing game – Dungeons & Dragons, a murder mystery or a cooperative game.",
        "You go step by step: people, class, background, ability scores, hit points, gear, spells, picture and story. Everything is saved as you type.",
        "Your game master can open the same figure with their own code and help you live – also from another place. You can write to each other on every step.",
        "When the figure is ready, it gets a short code. The game master uses it in Game Master (spilleder.itmartin.dk), and after the adventure hit points, gear and notes come back here.",
        "Peoples, classes and spells from the D&D 5e SRD 5.1 by Wizards of the Coast, used under CC-BY-4.0 (creativecommons.org/licenses/by/4.0). No ads.",
    ];
    k.HowTo =
    [
        "Open character.itmartin.dk, write your name and choose the kind of game. You get a personal code – keep it.",
        "Press Share with your game master and send the link. Then you both see the figure and every change.",
        "Go through the steps. When it is done, press Ready for adventure and give the figure code to your game master.",
    ];
    k.StaticPages = true;
    k.HealthChecks = sp => Task.FromResult<IReadOnlyList<KolibriHealthItem>>(
    [
        new("data", Directory.Exists(sp.GetRequiredService<IConfiguration>()["Character:DataDir"] ?? "/data")),
    ]);
});

var dataDir = builder.Configuration["Character:DataDir"] ?? "/data";
var apiKey = builder.Configuration["Character:ApiKey"] ?? "";
builder.Services.AddCharacterInfrastructure(dataDir, builder.Configuration);
builder.Services.AddCharacterApplication();

var app = builder.Build();
app.MapKolibri();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html") || ctx.File.Name == "figur-sw.js")
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});

// Two entrances to the same page, like Game Master: /player and /master.
foreach (var entrance in new[] { "/player", "/master" })
    app.MapGet(entrance, (HttpContext c) =>
    {
        c.Response.Headers.CacheControl = "no-cache";
        return Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html; charset=utf-8");
    });

var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// Behind the Cloudflare tunnel every request comes from cloudflared, so the visitor is in this header.
static string ClientIp(HttpContext c) =>
    c.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? c.Connection.RemoteIpAddress?.ToString() ?? "?";

// The page holds the figure's code (several figures on one phone: a game master helps more players) and sends it along.
// EventSource and <img> cannot send headers, so those two take it in the address.
(string Id, string Viewer)? Who(HttpContext c, Workshop w) =>
    w.WhoIs(c.Request.Headers["X-Kode"].FirstOrDefault() ?? c.Request.Query["kode"].FirstOrDefault());

IResult Fejl(OpenError e, string message = "") => e switch
{
    OpenError.Customer => Results.BadRequest(new { fejl = message }),
    OpenError.Locked => Results.Json(new { fejl = "Too many wrong tries. Wait 15 minutes and try again." }, statusCode: 429),
    OpenError.WrongCode => Results.Json(new { fejl = "That code is not right. Check it and try again." }, statusCode: 401),
    _ => Results.BadRequest(new { fejl = "Write your name." }),
};

IResult Done(FigureView? v) => v is null
    ? Results.Json(new { fejl = "This could not be saved. Maybe the figure is locked – ask the game master." }, statusCode: 403)
    : Results.Json(v, web);

app.MapPost("/api/ny", async (HttpContext c, NewBody b, Workshop w) =>
{
    var (o, e, m) = await w.CreateAsync(b.Navn, b.Slags, b.Kundenummer, ClientIp(c), c.RequestAborted);
    return o is null ? Fejl(e, m) : Results.Json(o, web);
});

app.MapPost("/api/aabn", (HttpContext c, CodeBody b, Workshop w) =>
{
    var (o, e) = w.Open(b.Kode, ClientIp(c));
    return o is null ? Fejl(e) : Results.Json(o, web);
});

app.MapPost("/api/mine", async (HttpContext c, CustomerBody b, Workshop w) =>
{
    var (list, e, m) = await w.MineAsync(b.Kundenummer, ClientIp(c), c.RequestAborted);
    return list is null
        ? e == OpenError.WrongCode ? Results.BadRequest(new { fejl = "Write your customer number – 8 digits." }) : Fejl(e, m)
        : Results.Json(list, web);
});

app.MapGet("/api/figur", (HttpContext c, Workshop w) =>
    Who(c, w) is { } x && w.View(x.Id, x.Viewer) is { } v ? Results.Json(v, web) : Results.Unauthorized());

// ---- the steps: every field is saved on its own as you type, so two people can work on one figure ----

app.MapPut("/api/figur/felt", (HttpContext c, FieldBody b, Workshop w) =>
    Who(c, w) is { } x ? Done(w.SetText(x.Id, x.Viewer, b.Felt ?? "", b.Vaerdi)) : Results.Unauthorized());

app.MapPut("/api/figur/slags", (HttpContext c, KindBody b, Workshop w) =>
    Who(c, w) is { } x ? Done(w.SetKind(x.Id, x.Viewer, b.Slags)) : Results.Unauthorized());

app.MapPut("/api/figur/trin", (HttpContext c, StepBody b, Workshop w) =>
    Who(c, w) is { } x ? (w.SetStep(x.Id, x.Viewer, b.Trin) is { } v ? Results.Json(v, web) : Results.Ok()) : Results.Unauthorized());

app.MapPut("/api/figur/evner", (HttpContext c, AbilityBody b, Workshop w) =>
    Who(c, w) is { } x ? Done(w.SetAbilities(x.Id, x.Viewer, b.Metode, b.Tal)) : Results.Unauthorized());

app.MapPost("/api/figur/rul", (HttpContext c, Workshop w) =>
    Who(c, w) is { } x ? Done(w.RollAbilities(x.Id, x.Viewer)) : Results.Unauthorized());

app.MapPut("/api/figur/liv", (HttpContext c, HpBody b, Workshop w) =>
    Who(c, w) is { } x ? Done(w.SetHp(x.Id, x.Viewer, b.Liv, b.MaxLiv)) : Results.Unauthorized());

app.MapPut("/api/figur/besvaergelser", (HttpContext c, List<SpellBody>? b, Workshop w) =>
    Who(c, w) is { } x ? Done(w.SetSpells(x.Id, x.Viewer, b?.Select(s => new Spell(s.Navn ?? "", s.Tekst ?? "")))) : Results.Unauthorized());

app.MapPost("/api/figur/klar", (HttpContext c, Workshop w) =>
{
    if (Who(c, w) is not { } x) return Results.Unauthorized();
    return w.MakeReady(x.Id, x.Viewer) is { } v ? Results.Json(v, web)
        : Results.BadRequest(new { fejl = "Give the character a name first (step Start)." });
});

app.MapPost("/api/figur/laas", (HttpContext c, LockBody b, Workshop w) =>
    Who(c, w) is { } x ? Done(w.SetLocked(x.Id, x.Viewer, b.Laast)) : Results.Unauthorized());

app.MapPost("/api/figur/kommentar", (HttpContext c, CommentBody b, Workshop w) =>
{
    if (Who(c, w) is not { } x) return Results.Unauthorized();
    return w.AddComment(x.Id, x.Viewer, b.Trin, b.Tekst) is { } k ? Results.Json(k, web) : Results.BadRequest(new { fejl = "Write a message." });
});

// The picture: the phone shrinks it to a small JPEG first, so it comes as the raw body.
app.MapPost("/api/figur/billede", async (HttpContext c, Workshop w) =>
{
    if (Who(c, w) is not { } x) return Results.Unauthorized();
    if (c.Request.ContentLength > FigureRules.MaxPictureBytes) return Results.BadRequest(new { fejl = "The picture is too big." });
    using var ms = new MemoryStream();
    await c.Request.Body.CopyToAsync(ms, c.RequestAborted);
    return w.SetPicture(x.Id, x.Viewer, ms.ToArray()) is { } v ? Results.Json(v, web)
        : Results.BadRequest(new { fejl = "The picture could not be saved. Try another one." });
});

app.MapGet("/api/billede", (HttpContext c, Workshop w) =>
{
    if (Who(c, w) is not { } x) return Results.Unauthorized();
    c.Response.Headers.CacheControl = "private, max-age=31536000";
    return w.Picture(x.Id) is { } b ? Results.File(b, "image/jpeg") : Results.NotFound();
});

// ---- spells from the free D&D 5e SRD ----

app.MapGet("/api/besvaergelser", async (HttpContext c, string? q, Workshop w, ISpellBook book) =>
{
    if (Who(c, w) is null) return Results.Unauthorized();
    try { return Results.Json(await book.SearchAsync(q ?? "", c.RequestAborted), web); }
    catch (Exception) { return Results.Json(new { fejl = "The spell look-up is not answering right now. Write what it does yourself." }, statusCode: 502); }
});

app.MapGet("/api/besvaergelser/{index}", async (HttpContext c, string index, Workshop w, ISpellBook book) =>
{
    if (Who(c, w) is null) return Results.Unauthorized();
    try { return await book.GetAsync(index, c.RequestAborted) is { } s ? Results.Json(s, web) : Results.NotFound(); }
    catch (Exception) { return Results.Json(new { fejl = "The spell look-up is not answering right now. Write what it does yourself." }, statusCode: 502); }
});

// ---- live: the player and the game master see each other's changes ----

app.MapGet("/api/live", async (HttpContext c, Workshop w, ILiveFeed live) =>
{
    if (Who(c, w) is not { } x) { c.Response.StatusCode = 401; return; }
    c.Response.Headers.ContentType = "text/event-stream";
    c.Response.Headers.CacheControl = "no-cache";
    c.Response.Headers["X-Accel-Buffering"] = "no";
    var (id, reader) = live.Join(x.Id, x.Viewer);
    // Who is here right now: "the game master is looking" on the player's screen and the other way round.
    live.Send(x.Id, "her", new { viewer = x.Viewer, here = true }, v => v != x.Viewer);
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
    finally
    {
        live.Leave(id);
        live.Send(x.Id, "her", new { viewer = x.Viewer, here = false }, v => v != x.Viewer);
    }
});

// The other one asks "are you here?" after (re)connecting; whoever is here answers with a "her".
app.MapPost("/api/her", (HttpContext c, Workshop w, ILiveFeed live) =>
{
    if (Who(c, w) is not { } x) return Results.Unauthorized();
    live.Send(x.Id, "her", new { viewer = x.Viewer, here = true }, v => v != x.Viewer);
    return Results.Ok();
});

// ---- Game Master (server to server, Character:ApiKey on both sides) ----

bool KeyOk(HttpContext c) => apiKey.Length >= 16 &&
    CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(c.Request.Headers["X-Api-Key"].FirstOrDefault() ?? ""), Encoding.UTF8.GetBytes(apiKey));

app.MapGet("/api/eventyr/{kode}", (HttpContext c, string kode, Workshop w) =>
    !KeyOk(c) ? Results.StatusCode(403) : w.ForAdventure(kode) is { } a ? Results.Json(a, web) : Results.NotFound());

app.MapGet("/api/eventyr/{kode}/billede", (HttpContext c, string kode, Workshop w) =>
    !KeyOk(c) ? Results.StatusCode(403) : w.AdventurePicture(kode) is { } b ? Results.File(b, "image/jpeg") : Results.NotFound());

app.MapPost("/api/eventyr/{kode}", (HttpContext c, string kode, BackBody b, Workshop w) =>
    !KeyOk(c) ? Results.StatusCode(403) : w.BackFromAdventure(kode, b.Hp, b.MaxHp, b.Gear, b.Notes) ? Results.Ok() : Results.NotFound());

app.Run();

public sealed record NewBody(string? Navn, string? Slags, string? Kundenummer);
public sealed record CodeBody(string? Kode);
public sealed record CustomerBody(string? Kundenummer);
public sealed record FieldBody(string? Felt, string? Vaerdi);
public sealed record KindBody(string? Slags);
public sealed record StepBody(string? Trin);
public sealed record AbilityBody(string? Metode, int[]? Tal);
public sealed record HpBody(int Liv, int MaxLiv);
public sealed record SpellBody(string? Navn, string? Tekst);
public sealed record LockBody(bool Laast);
public sealed record CommentBody(string? Trin, string? Tekst);
public sealed record BackBody(int Hp, int MaxHp, string? Gear, string? Notes);

public partial class Program;
