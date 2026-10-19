using System.Text.Json;
using ITMartin.Shared.UI.Kolibri;
using ITMartinSnak.Application;
using ITMartinSnak.Domain;
using ITMartinSnak.Infrastructure;

// Snak (Kolibri Kommunikere) - 2026-10-09, user: "a quick app for 4 people where we can chat ... instead of
// using fb groups", push on by default, home-screen guide, and an admin overview: Martin makes the tasks,
// the others ONLY chat and see the tasks and put themselves on one.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Snak";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Jeres egen lille gruppe-chat – uden Facebook og uden reklamer.";
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
    k.About =
    [
        "Snak er en lille, privat chat for jeres gruppe - i stedet for en Facebook-gruppe. Kun dem med gruppens kode kan komme ind.",
        "Der er ingen reklamer, og intet bliver solgt eller delt. Under Opgaver lægger gruppens tovholder opgaver ud, og alle kan se, hvem der tager hvad.",
    ];
    k.HowTo =
    [
        "Åbn snak.itmartin.dk og skriv gruppens kode og det navn, de andre skal se.",
        "Skriv i chatten. Slå beskeder til, så får du besked, når der skrives.",
        "Læg Snak på telefonen, så den åbner som en app.",
    ];
    // Snak's page is a static index.html, so /om and /hjaelp come as plain HTML (2026-10-19: they gave 404).
    k.StaticPages = true;
    k.HealthChecks = sp => Task.FromResult<IReadOnlyList<KolibriHealthItem>>(
    [
        new("data", Directory.Exists(sp.GetRequiredService<IConfiguration>()["Snak:DataDir"] ?? "/data")),
    ]);
});

// DDD/Clean since 2026-10-12: the endpoints below only call Application (Group, Access).
var dataDir = builder.Configuration["Snak:DataDir"] ?? "/data";
builder.Services.AddSnakApplication(builder.Configuration["Snak:Code"] ?? "", builder.Configuration["Snak:AdminCode"] ?? "");
builder.Services.AddSnakInfrastructure(dataDir);

var app = builder.Build();
app.MapKolibri();
app.UseDefaultFiles();
// The page and the service worker must always be fetched fresh, or phones keep showing an old version
// (css/js carry ?v= in index.html, so they can be cached).
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        if (name.EndsWith(".html") || name == "snak-sw.js")
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});

var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
bool IsAdmin(HttpContext c, Access a) => a.IsAdmin(c.Request.Cookies["snak_admin"]);
bool IsMember(HttpContext c, Access a) => a.IsMember(c.Request.Cookies["snak"], c.Request.Cookies["snak_admin"]);

// Behind the Cloudflare tunnel every request comes from cloudflared, so the visitor is in this header.
static string ClientIp(HttpContext c) =>
    c.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? c.Connection.RemoteIpAddress?.ToString() ?? "?";

app.MapPost("/api/login", (HttpContext c, LoginBody b, Access access) =>
{
    var result = access.Login(b.Kode, ClientIp(c));
    if (result == LoginResult.Locked)
        return Results.Json(new { fejl = "For mange forkerte forsøg. Vent et kvarter og prøv igen." }, statusCode: 429);
    if (result == LoginResult.Wrong)
        return Results.Json(new { fejl = "Koden passer ikke. Spørg den, der inviterede dig." }, statusCode: 401);
    var opts = new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(400) };
    c.Response.Cookies.Append("snak", access.MemberToken, opts);
    var admin = result == LoginResult.Admin;
    if (admin) c.Response.Cookies.Append("snak_admin", access.AdminToken, opts);
    return Results.Ok(new { admin });
});

app.MapPost("/api/logout", (HttpContext c) =>
{
    c.Response.Cookies.Delete("snak");
    c.Response.Cookies.Delete("snak_admin");
    return Results.Ok();
});

app.MapGet("/api/me", (HttpContext c, Access access, IPushSender push) =>
    IsMember(c, access) ? Results.Ok(new { admin = IsAdmin(c, access), pushKey = push.PublicKey }) : Results.Unauthorized());

// ---- chat ----

app.MapGet("/api/beskeder", (HttpContext c, Access access, Group group, long? efter) =>
    IsMember(c, access) ? Results.Json(group.MessagesAfter(efter ?? 0), web) : Results.Unauthorized());

app.MapPost("/api/beskeder", (HttpContext c, MessageBody b, Access access, Group group) =>
{
    if (!IsMember(c, access)) return Results.Unauthorized();
    var m = group.Write(b.Navn, b.Tekst, b.Endpoint);
    return m is null ? Results.BadRequest(new { fejl = "Skriv dit navn og en besked." }) : Results.Json(m, web);
});

// ---- tasks: everyone sees them and can put themselves on one; only the admin makes, edits and removes ----

app.MapGet("/api/opgaver", (HttpContext c, Access access, Group group) =>
    IsMember(c, access) ? Results.Json(group.Tasks(), web) : Results.Unauthorized());

app.MapPost("/api/opgaver", (HttpContext c, TaskBody b, Access access, Group group) =>
{
    if (!IsAdmin(c, access)) return Results.StatusCode(403);
    var t = group.AddTask(b.Titel, b.Note, b.Hvornaar, b.Endpoint);
    return t is null ? Results.BadRequest(new { fejl = "Opgaven skal have en titel." }) : Results.Json(t, web);
});

app.MapPut("/api/opgaver/{id:long}", (HttpContext c, long id, TaskBody b, Access access, Group group) =>
{
    if (!IsAdmin(c, access)) return Results.StatusCode(403);
    var t = group.EditTask(id, b.Titel, b.Note, b.Hvornaar);
    return t is null ? Results.BadRequest(new { fejl = "Opgaven findes ikke, eller titlen mangler." }) : Results.Json(t, web);
});

app.MapPost("/api/opgaver/{id:long}/faerdig", (HttpContext c, long id, DoneBody b, Access access, Group group) =>
{
    if (!IsAdmin(c, access)) return Results.StatusCode(403);
    return group.SetDone(id, b.Faerdig) is null ? Results.NotFound() : Results.Ok();
});

app.MapDelete("/api/opgaver/{id:long}", (HttpContext c, long id, Access access, Group group) =>
{
    if (!IsAdmin(c, access)) return Results.StatusCode(403);
    return group.DeleteTask(id) ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/opgaver/{id:long}/tag", (HttpContext c, long id, TakeBody b, Access access, Group group) =>
{
    if (!IsMember(c, access)) return Results.Unauthorized();
    var t = group.ToggleTaker(id, b.Navn, b.Endpoint);
    return t is null ? Results.BadRequest(new { fejl = "Skriv dit navn først." }) : Results.Json(t, web);
});

// ---- live updates + push ----

app.MapGet("/api/live", async (HttpContext c, Access access, ILiveFeed live) =>
{
    if (!IsMember(c, access)) { c.Response.StatusCode = 401; return; }
    c.Response.Headers.ContentType = "text/event-stream";
    c.Response.Headers.CacheControl = "no-cache";
    c.Response.Headers["X-Accel-Buffering"] = "no";
    var (id, reader) = live.Join();
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

app.MapPost("/api/push", (HttpContext c, PushBody b, Access access, IPushSender push) =>
{
    if (!IsMember(c, access)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(b.Endpoint) || !b.Endpoint.StartsWith("https://") ||
        string.IsNullOrWhiteSpace(b.P256dh) || string.IsNullOrWhiteSpace(b.Auth))
        return Results.BadRequest();
    push.Subscribe(b.Endpoint, b.P256dh, b.Auth, GroupRules.CleanName(b.Navn));
    return Results.Ok();
});

app.MapPost("/api/push/fra", (HttpContext c, PushBody b, Access access, IPushSender push) =>
{
    if (!IsMember(c, access)) return Results.Unauthorized();
    if (!string.IsNullOrWhiteSpace(b.Endpoint)) push.Unsubscribe(b.Endpoint);
    return Results.Ok();
});

app.Run();

public sealed record LoginBody(string? Kode);
public sealed record MessageBody(string? Navn, string? Tekst, string? Endpoint);
public sealed record TaskBody(string? Titel, string? Note, string? Hvornaar, string? Endpoint);
public sealed record DoneBody(bool Faerdig);
public sealed record TakeBody(string? Navn, string? Endpoint);
public sealed record PushBody(string? Endpoint, string? P256dh, string? Auth, string? Navn);

public partial class Program;
