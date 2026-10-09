using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ITMartin.Shared.UI.Kolibri;
using ITMartinSnak.Server;

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
    k.OwnOmPage = true;
    k.OwnHjaelpPage = true;
    k.HealthChecks = sp => Task.FromResult<IReadOnlyList<KolibriHealthItem>>(
    [
        new("data", Directory.Exists(sp.GetRequiredService<IConfiguration>()["Snak:DataDir"] ?? "/data")),
    ]);
});

var dataDir = builder.Configuration["Snak:DataDir"] ?? "/data";
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new Store(dataDir, sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(sp => new PushStore(dataDir, sp.GetRequiredService<ILogger<PushStore>>()));
builder.Services.AddSingleton<Live>();

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

var code = app.Configuration["Snak:Code"] ?? "";
var adminCode = app.Configuration["Snak:AdminCode"] ?? "";
static string Token(string secret, string kind) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kind + ":" + secret)));
var memberToken = Token(code, "medlem");
var adminToken = Token(adminCode, "admin");
bool IsAdmin(HttpContext c) => adminCode.Length >= 6 && c.Request.Cookies["snak_admin"] == adminToken;
bool IsMember(HttpContext c) => IsAdmin(c) || (code.Length >= 4 && c.Request.Cookies["snak"] == memberToken);

// Behind the Cloudflare tunnel every request comes from cloudflared, so the visitor is in this header.
static string ClientIp(HttpContext c) =>
    c.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? c.Connection.RemoteIpAddress?.ToString() ?? "?";
var wrongTries = new ConcurrentDictionary<string, (int count, DateTime first)>();

app.MapPost("/api/login", (HttpContext c, LoginBody b) =>
{
    var ip = ClientIp(c);
    if (wrongTries.TryGetValue(ip, out var w) && w.count >= 10 && DateTime.UtcNow - w.first < TimeSpan.FromMinutes(15))
        return Results.Json(new { fejl = "For mange forkerte forsøg. Vent et kvarter og prøv igen." }, statusCode: 429);

    var typed = (b.Kode ?? "").Trim();
    var admin = adminCode.Length >= 6 && typed == adminCode;
    if (!admin && (code.Length < 4 || typed != code))
    {
        wrongTries.AddOrUpdate(ip, _ => (1, DateTime.UtcNow),
            (_, old) => DateTime.UtcNow - old.first > TimeSpan.FromMinutes(15) ? (1, DateTime.UtcNow) : (old.count + 1, old.first));
        return Results.Json(new { fejl = "Koden passer ikke. Spørg den, der inviterede dig." }, statusCode: 401);
    }
    wrongTries.TryRemove(ip, out _);
    var opts = new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(400) };
    c.Response.Cookies.Append("snak", memberToken, opts);
    if (admin) c.Response.Cookies.Append("snak_admin", adminToken, opts);
    return Results.Ok(new { admin });
});

app.MapPost("/api/logout", (HttpContext c) =>
{
    c.Response.Cookies.Delete("snak");
    c.Response.Cookies.Delete("snak_admin");
    return Results.Ok();
});

app.MapGet("/api/me", (HttpContext c, PushStore push) =>
    IsMember(c) ? Results.Ok(new { admin = IsAdmin(c), pushKey = push.PublicKey }) : Results.Unauthorized());

// ---- chat ----

app.MapGet("/api/beskeder", (HttpContext c, Store store, long? efter) =>
    IsMember(c) ? Results.Json(store.MessagesAfter(efter ?? 0), Json.Web) : Results.Unauthorized());

app.MapPost("/api/beskeder", (HttpContext c, MessageBody b, Store store, Live live, PushStore push) =>
{
    if (!IsMember(c)) return Results.Unauthorized();
    var m = store.AddMessage(b.Navn, b.Tekst);
    if (m is null) return Results.BadRequest(new { fejl = "Skriv dit navn og en besked." });
    live.Send("besked", m);
    push.SendToOthers(b.Endpoint, m.Name, m.Text, "snak-besked");
    return Results.Json(m, Json.Web);
});

// ---- tasks: everyone sees them and can put themselves on one; only the admin makes, edits and removes ----

app.MapGet("/api/opgaver", (HttpContext c, Store store) =>
    IsMember(c) ? Results.Json(store.Tasks(), Json.Web) : Results.Unauthorized());

app.MapPost("/api/opgaver", (HttpContext c, TaskBody b, Store store, Live live, PushStore push) =>
{
    if (!IsAdmin(c)) return Results.StatusCode(403);
    var t = store.AddTask(b.Titel, b.Note, b.Hvornaar);
    if (t is null) return Results.BadRequest(new { fejl = "Opgaven skal have en titel." });
    live.Send("opgaver", store.Tasks());
    push.SendToOthers(b.Endpoint, "Ny opgave", t.Title + (t.When.Length > 0 ? " · " + t.When : ""), "snak-opgave");
    return Results.Json(t, Json.Web);
});

app.MapPut("/api/opgaver/{id:long}", (HttpContext c, long id, TaskBody b, Store store, Live live) =>
{
    if (!IsAdmin(c)) return Results.StatusCode(403);
    var t = store.EditTask(id, b.Titel, b.Note, b.Hvornaar);
    if (t is null) return Results.BadRequest(new { fejl = "Opgaven findes ikke, eller titlen mangler." });
    live.Send("opgaver", store.Tasks());
    return Results.Json(t, Json.Web);
});

app.MapPost("/api/opgaver/{id:long}/faerdig", (HttpContext c, long id, DoneBody b, Store store, Live live) =>
{
    if (!IsAdmin(c)) return Results.StatusCode(403);
    if (store.SetDone(id, b.Faerdig) is null) return Results.NotFound();
    live.Send("opgaver", store.Tasks());
    return Results.Ok();
});

app.MapDelete("/api/opgaver/{id:long}", (HttpContext c, long id, Store store, Live live) =>
{
    if (!IsAdmin(c)) return Results.StatusCode(403);
    if (!store.DeleteTask(id)) return Results.NotFound();
    live.Send("opgaver", store.Tasks());
    return Results.Ok();
});

app.MapPost("/api/opgaver/{id:long}/tag", (HttpContext c, long id, TakeBody b, Store store, Live live, PushStore push) =>
{
    if (!IsMember(c)) return Results.Unauthorized();
    var t = store.ToggleTaker(id, b.Navn);
    if (t is null) return Results.BadRequest(new { fejl = "Skriv dit navn først." });
    live.Send("opgaver", store.Tasks());
    var name = Store.CleanName(b.Navn);
    if (t.Takers.Contains(name, StringComparer.OrdinalIgnoreCase))
        push.SendToOthers(b.Endpoint, "Opgave taget", $"{name} tager: {t.Title}", "snak-opgave");
    return Results.Json(t, Json.Web);
});

// ---- live updates + push ----

app.MapGet("/api/live", async (HttpContext c, Live live) =>
{
    if (!IsMember(c)) { c.Response.StatusCode = 401; return; }
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

app.MapPost("/api/push", (HttpContext c, PushBody b, PushStore push) =>
{
    if (!IsMember(c)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(b.Endpoint) || !b.Endpoint.StartsWith("https://") ||
        string.IsNullOrWhiteSpace(b.P256dh) || string.IsNullOrWhiteSpace(b.Auth))
        return Results.BadRequest();
    push.Add(new PushStore.Subscriber(b.Endpoint, b.P256dh, b.Auth, Store.CleanName(b.Navn), DateTime.UtcNow));
    return Results.Ok();
});

app.MapPost("/api/push/fra", (HttpContext c, PushBody b, PushStore push) =>
{
    if (!IsMember(c)) return Results.Unauthorized();
    if (!string.IsNullOrWhiteSpace(b.Endpoint)) push.Remove(b.Endpoint);
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
