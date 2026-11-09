using System.Text.Json;
using ITMartin.Shared.UI.Kolibri;
using ITMartinSnak.Application;
using ITMartinSnak.Domain;
using ITMartinSnak.Infrastructure;

// Snak (Kolibri Kommunikere) - 2026-10-09, user: "a quick app for 4 people where we can chat ... instead of
// using fb groups", push on by default, home-screen guide, Martin makes the tasks, the others take them.
// 2026-11-04 ROOMS: "a snak with every customer", "temporary chats, which will be removed after they are finished".
// One room per customer or job; the link /r/<key> is the way in; Martin (admin code) sees every room.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Snak";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Ét rum til hver sag – chat og opgaver samlet ét sted, uden Facebook og uden reklamer.";
    k.Version = "2026.11";
    k.ThemeColor = "#f4f6f5";
    k.About =
    [
        "Snak er et lille, privat rum til én sag: dig og Martin (og dem, du vil have med). Alt om sagen står ét sted – beskeder og opgaver.",
        "Kun dem med rummets link kan komme ind. Når sagen er klaret, bliver rummet slettet efter 14 dage. Der er ingen reklamer, og intet bliver solgt eller delt.",
    ];
    k.HowTo =
    [
        "Åbn det link, du har fået af Martin, og skriv dit navn.",
        "Skriv i chatten. Slå beskeder til, så får du besked, når Martin svarer.",
        "Læg Snak på telefonen, så åbner rummet som en app.",
    ];
    // Snak's page is a static index.html, so /om and /hjaelp come as plain HTML (2026-10-19: they gave 404).
    k.StaticPages = true;
    k.HealthChecks = sp => Task.FromResult<IReadOnlyList<KolibriHealthItem>>(
    [
        new("data", Directory.Exists(sp.GetRequiredService<IConfiguration>()["Snak:DataDir"] ?? "/data")),
    ]);
});

// DDD/Clean since 2026-10-12: the endpoints below only call Application (Rooms, Group, Access).
var dataDir = builder.Configuration["Snak:DataDir"] ?? "/data";
var publicUrl = builder.Configuration["Snak:PublicUrl"] ?? "https://snak.itmartin.dk";
builder.Services.AddSnakApplication(builder.Configuration["Snak:AdminCode"] ?? "", builder.Configuration["Snak:ApiKey"] ?? "", publicUrl);
builder.Services.AddSnakInfrastructure(dataDir);
builder.Services.AddHostedService<FinishedRoomsSweeper>();

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
const string HostName = "Martin";
bool IsAdmin(HttpContext c, Access a) => a.IsAdmin(c.Request.Cookies["snak_admin"]);

// Behind the Cloudflare tunnel every request comes from cloudflared, so the visitor is in this header.
static string ClientIp(HttpContext c) =>
    c.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? c.Connection.RemoteIpAddress?.ToString() ?? "?";

static bool GoodPush(PushBody b) =>
    !string.IsNullOrWhiteSpace(b.Endpoint) && b.Endpoint.StartsWith("https://") && !string.IsNullOrWhiteSpace(b.P256dh) && !string.IsNullOrWhiteSpace(b.Auth);

// A room's page: the same index.html, never cached; the script reads the key from the address.
app.MapGet("/r/{key}", (HttpContext c, IWebHostEnvironment env) =>
{
    c.Response.Headers.CacheControl = "no-cache";
    return Results.File(Path.Combine(env.WebRootPath, "index.html"), "text/html; charset=utf-8");
});

// ---- Martin ----

app.MapPost("/api/login", (HttpContext c, LoginBody b, Access access) =>
{
    var result = access.Login(b.Kode, ClientIp(c));
    if (result == LoginResult.Locked)
        return Results.Json(new { fejl = "For mange forkerte forsøg. Vent et kvarter og prøv igen." }, statusCode: 429);
    if (result == LoginResult.Wrong)
        return Results.Json(new { fejl = "Koden passer ikke." }, statusCode: 401);
    c.Response.Cookies.Append("snak_admin", access.AdminToken,
        new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(400) });
    return Results.Ok(new { admin = true });
});

app.MapPost("/api/logout", (HttpContext c) =>
{
    c.Response.Cookies.Delete("snak");
    c.Response.Cookies.Delete("snak_admin");
    return Results.Ok();
});

app.MapGet("/api/me", (HttpContext c, Access access, IPushSender push) =>
    Results.Ok(new { admin = IsAdmin(c, access), pushKey = push.PublicKey, vaert = HostName }));

app.MapGet("/api/rum", (HttpContext c, Access access, Rooms rooms) =>
    IsAdmin(c, access)
        ? Results.Json(rooms.Overview().Select(r => new
        {
            r.Id, r.Title, r.CustomerRef, r.CreatedAt, r.LastAt, r.DoneAt, r.DeleteAt, r.Waiting, r.LastName, r.LastText, r.Gruppe,
            Link = $"{publicUrl}/r/{r.Key}",
        }), web)
        : Results.Unauthorized());

// Martin makes a room - or Kunder / itmartin.dk with the API key (header X-Snak-Key), optionally with the first message.
app.MapPost("/api/rum", (HttpContext c, RoomBody b, Access access, Rooms rooms) =>
{
    if (!IsAdmin(c, access) && !access.IsApi(c.Request.Headers["X-Snak-Key"].FirstOrDefault())) return Results.Unauthorized();
    try
    {
        var r = rooms.Create(b.Titel, b.Kunde, b.Gruppe);
        if (!string.IsNullOrWhiteSpace(b.Besked)) rooms.GroupOf(r).Write(string.IsNullOrWhiteSpace(b.Navn) ? "Kunde" : b.Navn, b.Besked);
        return Results.Json(new { id = r.Id, titel = r.Title, link = rooms.Link(r) }, web);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { fejl = ex.Message }); }
});

app.MapPost("/api/rum/{id}/klaret", (HttpContext c, string id, Access access, Rooms rooms) =>
    !IsAdmin(c, access) ? Results.StatusCode(403) : rooms.MarkDone(id, HostName) ? Results.Ok() : Results.NotFound());

app.MapPost("/api/rum/{id}/genaabn", (HttpContext c, string id, Access access, Rooms rooms) =>
    !IsAdmin(c, access) ? Results.StatusCode(403) : rooms.Reopen(id) ? Results.Ok() : Results.NotFound());

app.MapPut("/api/rum/{id}", (HttpContext c, string id, RoomBody b, Access access, Rooms rooms) =>
    !IsAdmin(c, access) ? Results.StatusCode(403) : rooms.Rename(id, b.Titel) ? Results.Ok() : Results.NotFound());

app.MapDelete("/api/rum/{id}", (HttpContext c, string id, Access access, Rooms rooms) =>
    !IsAdmin(c, access) ? Results.StatusCode(403) : rooms.Delete(id) ? Results.Ok() : Results.NotFound());

// Martin's phone gets a push from every room.
app.MapPost("/api/push", (HttpContext c, PushBody b, Access access, IPushSender push) =>
{
    if (!IsAdmin(c, access)) return Results.Unauthorized();
    if (!GoodPush(b)) return Results.BadRequest();
    push.Subscribe("", b.Endpoint!, b.P256dh!, b.Auth!, HostName);
    return Results.Ok();
});

app.MapPost("/api/push/fra", (PushBody b, IPushSender push) =>
{
    if (!string.IsNullOrWhiteSpace(b.Endpoint)) push.Unsubscribe(b.Endpoint);
    return Results.Ok();
});

// ---- one room: the key in the address is the way in ----

var room = app.MapGroup("/api/r/{key}");

room.MapGet("", (HttpContext c, string key, Rooms rooms, Access access, IPushSender push) =>
    rooms.ByKey(key) is { } r
        ? Results.Json(new { id = r.Id, titel = r.Title, gruppe = r.Gruppe, klaret = r.IsDone, sletDato = r.DeleteAt, admin = IsAdmin(c, access), pushKey = push.PublicKey, vaert = HostName }, web)
        : Results.Json(new { fejl = "Rummet findes ikke (mere)." }, statusCode: 404));

room.MapGet("/beskeder", (string key, Rooms rooms, long? efter) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).MessagesAfter(efter ?? 0), web) : Results.NotFound());

room.MapPost("/beskeder", (HttpContext c, string key, MessageBody b, Rooms rooms, Access access) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var host = IsAdmin(c, access);
    var m = rooms.GroupOf(r).Write(host ? HostName : b.Navn, b.Tekst, b.Endpoint, host);
    return m is null ? Results.BadRequest(new { fejl = "Skriv dit navn og en besked." }) : Results.Json(m, web);
});

// Tasks: everyone in the room sees them and can put themselves on one; only Martin makes, edits and removes.
room.MapGet("/opgaver", (string key, Rooms rooms) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).Tasks(), web) : Results.NotFound());

room.MapPost("/opgaver", (HttpContext c, string key, TaskBody b, Access access, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!IsAdmin(c, access)) return Results.StatusCode(403);
    var t = rooms.GroupOf(r).AddTask(b.Titel, b.Note, b.Hvornaar, b.Endpoint);
    return t is null ? Results.BadRequest(new { fejl = "Opgaven skal have en titel." }) : Results.Json(t, web);
});

room.MapPut("/opgaver/{id:long}", (HttpContext c, string key, long id, TaskBody b, Access access, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!IsAdmin(c, access)) return Results.StatusCode(403);
    var t = rooms.GroupOf(r).EditTask(id, b.Titel, b.Note, b.Hvornaar);
    return t is null ? Results.BadRequest(new { fejl = "Opgaven findes ikke, eller titlen mangler." }) : Results.Json(t, web);
});

room.MapPost("/opgaver/{id:long}/faerdig", (HttpContext c, string key, long id, DoneBody b, Access access, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!IsAdmin(c, access)) return Results.StatusCode(403);
    return rooms.GroupOf(r).SetDone(id, b.Faerdig) is null ? Results.NotFound() : Results.Ok();
});

room.MapDelete("/opgaver/{id:long}", (HttpContext c, string key, long id, Access access, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!IsAdmin(c, access)) return Results.StatusCode(403);
    return rooms.GroupOf(r).DeleteTask(id) ? Results.Ok() : Results.NotFound();
});

room.MapPost("/opgaver/{id:long}/tag", (string key, long id, TakeBody b, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var t = rooms.GroupOf(r).ToggleTaker(id, b.Navn, b.Endpoint);
    return t is null ? Results.BadRequest(new { fejl = "Skriv dit navn først." }) : Results.Json(t, web);
});

// Documents: everyone in the room adds and downloads; the one who added it (same name) or Martin removes it.
room.MapGet("/filer", (string key, Rooms rooms) =>
    rooms.ByKey(key) is { } r ? Results.Json(rooms.GroupOf(r).Files().Select(FileView), web) : Results.NotFound());

// multipart/form-data: fil + navn (+ endpoint). The form is read by hand, so no antiforgery token is needed (same as the
// JSON endpoints - the room key in the address is the access).
room.MapPost("/filer", async (HttpContext c, string key, Rooms rooms, Access access) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!c.Request.HasFormContentType) return Results.BadRequest(new { fejl = "Vælg en fil." });
    var form = await c.Request.ReadFormAsync(c.RequestAborted);
    if (form.Files.GetFile("fil") is not { } f) return Results.BadRequest(new { fejl = "Vælg en fil." });
    var host = IsAdmin(c, access);
    await using var s = f.OpenReadStream();
    var (file, err) = await rooms.GroupOf(r).AddFileAsync(f.FileName, f.Length, s, host ? HostName : form["navn"].ToString(),
        form["endpoint"].ToString(), c.RequestAborted);
    return file is null ? Results.BadRequest(new { fejl = err }) : Results.Json(FileView(file), web);
});

room.MapGet("/filer/{id:long}", (HttpContext c, string key, long id, Rooms rooms) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    var g = rooms.GroupOf(r);
    if (g.File(id) is not { } f || g.OpenFile(f) is not { } s) return Results.NotFound();
    c.Response.Headers.XContentTypeOptions = "nosniff";
    c.Response.Headers.CacheControl = "private, max-age=3600";
    // PDF and pictures open in the phone's viewer; everything else is saved.
    var inline = f.ContentType == "application/pdf" || f.ContentType.StartsWith("image/");
    var disp = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue(inline ? "inline" : "attachment");
    disp.SetHttpFileName(f.Name);
    c.Response.Headers.ContentDisposition = disp.ToString();
    return Results.Stream(s, f.ContentType, enableRangeProcessing: true);
});

room.MapDelete("/filer/{id:long}", (HttpContext c, string key, long id, string? navn, Rooms rooms, Access access) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    return rooms.GroupOf(r).DeleteFile(id, navn, IsAdmin(c, access)) ? Results.Ok() : Results.StatusCode(403);
});

room.MapGet("/live",async (HttpContext c, string key, Rooms rooms, ILiveFeed live) =>
{
    if (rooms.ByKey(key) is not { } r) { c.Response.StatusCode = 404; return; }
    c.Response.Headers.ContentType = "text/event-stream";
    c.Response.Headers.CacheControl = "no-cache";
    c.Response.Headers["X-Accel-Buffering"] = "no";
    var (id, reader) = live.Join(r.Id);
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

room.MapPost("/push", (string key, PushBody b, Rooms rooms, IPushSender push) =>
{
    if (rooms.ByKey(key) is not { } r) return Results.NotFound();
    if (!GoodPush(b)) return Results.BadRequest();
    push.Subscribe(r.Id, b.Endpoint!, b.P256dh!, b.Auth!, GroupRules.CleanName(b.Navn));
    return Results.Ok();
});

app.Run();

// What the page sees of a file - never the name on disk.
static object FileView(SharedFile f) => new { f.Id, f.Name, f.Size, f.ContentType, f.By, f.At };

public sealed record LoginBody(string? Kode);
public sealed record RoomBody(string? Titel, string? Kunde, string? Navn, string? Besked, bool Gruppe = false);
public sealed record MessageBody(string? Navn, string? Tekst, string? Endpoint);
public sealed record TaskBody(string? Titel, string? Note, string? Hvornaar, string? Endpoint);
public sealed record DoneBody(bool Faerdig);
public sealed record TakeBody(string? Navn, string? Endpoint);
public sealed record PushBody(string? Endpoint, string? P256dh, string? Auth, string? Navn);

// Every hour: rooms marked "klaret" more than 14 days ago are deleted (messages, tasks, phones).
public sealed class FinishedRoomsSweeper(Rooms rooms, ILogger<FinishedRoomsSweeper> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try { var n = rooms.DeleteFinished(); if (n > 0) log.LogInformation("Deleted {Count} finished rooms", n); }
            catch (Exception ex) { log.LogWarning(ex, "Deleting finished rooms failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}

public partial class Program;
