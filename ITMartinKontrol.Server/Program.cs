using System.Text.Json;
using ITMartinKontrol.Server;
using ITMartin.Shared.UI.Kolibri;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DockerClient>();
builder.Services.AddSingleton<Sampler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Sampler>());
builder.Services.AddSingleton<PushStore>();
builder.Services.AddSingleton<AlarmService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AlarmService>());
builder.Services.AddHttpClient("peer", c => c.Timeout = TimeSpan.FromSeconds(8));

builder.Services.AddKolibri(k =>
{
    k.Name = "Kontrol";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Alle apps og servere på ét sted - tænd, sluk, og få besked når noget falder.";
    k.Version = "2026.09";
    k.ThemeColor = "#0a0d14";
    k.OwnOmPage = true;
    k.OwnHjaelpPage = true;
    k.HealthChecks = sp =>
    {
        var b = BackupStatus.Read(sp.GetRequiredService<IConfiguration>());
        return Task.FromResult<IReadOnlyList<KolibriHealthItem>>([new KolibriHealthItem("backup", !b.Stale, b.LastRun ?? "ingen")]);
    };
});

var app = builder.Build();
app.MapKolibri();
var JsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.UseDefaultFiles();
app.UseStaticFiles();

var cfg = app.Configuration;
var hostName = cfg["Kontrol:HostName"] ?? Environment.MachineName;
// Containers that must never be stopped from the UI - stopping them would
// take the UI (or the tunnel) down with them.
var protectedNames = cfg.GetSection("Kontrol:Protected").Get<string[]>() ?? ["kontrol-web", "cloudflared"];
// Other Kontrol instances (photoserver). Same image, same PIN; the NAS one
// shows them all. Kontrol__Peers__0__Name / __Url in compose.
var peers = cfg.GetSection("Kontrol:Peers").Get<List<Peer>>() ?? [];
var pin = cfg["Kontrol:Pin"];
if (string.IsNullOrWhiteSpace(pin))
    app.Logger.LogWarning("Kontrol__Pin is not set - start/stop endpoints will refuse every request.");

// Admin PIN = everything. Limited PINs (Kontrol__Limited__0__Pin / __Apps__0 in compose) may
// only start/stop/restart the containers listed for them - e.g. a family member who is
// allowed to switch star-realms-web on and off, nothing else.
var limited = cfg.GetSection("Kontrol:Limited").Get<List<LimitedPin>>() ?? [];

// null = not authorised; empty = admin (all apps); otherwise the allowed container names.
string[]? PinScope(HttpRequest req)
{
    if (!req.Headers.TryGetValue("X-Kontrol-Pin", out var v)) return null;
    var given = v.ToString();
    if (!string.IsNullOrWhiteSpace(pin) && string.Equals(given, pin, StringComparison.Ordinal)) return [];
    var l = limited.FirstOrDefault(x => string.Equals(x.Pin, given, StringComparison.Ordinal));
    return l?.Apps ?? null;
}
bool PinOk(HttpRequest req) => PinScope(req) is { Length: 0 };

// One host as the UI sees it: live container list + usage + 48 h history.
async Task<HostView> LocalViewAsync(DockerClient docker, Sampler sampler, CancellationToken ct)
{
    var containers = await docker.ListAllAsync(ct);
    var usage = sampler.LastContainers;
    var hist = sampler.History();
    return new HostView(hostName, true, null, sampler.LastHost, sampler.LastAt,
        containers.Select(c => new ContainerView(c.Name, c.Status, c.Running, c.Image, c.Ports,
            protectedNames.Contains(c.Name, StringComparer.Ordinal),
            usage.TryGetValue(c.Name, out var u) ? u.CpuPct : null,
            usage.TryGetValue(c.Name, out var u2) ? u2.MemMb : null,
            hist.Select(s => s.C.TryGetValue(c.Name, out var v) ? v[0] : 0).ToArray())).ToList(),
        new History(hist.Select(s => s.T).ToArray(), hist.Select(s => s.Cpu).ToArray(), hist.Select(s => s.Mem).ToArray()));
}

app.MapGet("/api/hosts", async (DockerClient docker, Sampler sampler, IHttpClientFactory f, CancellationToken ct) =>
{
    var views = new List<HostView> { await LocalViewAsync(docker, sampler, ct) };
    foreach (var p in peers)
    {
        try
        {
            var json = await f.CreateClient("peer").GetStringAsync(p.Url.TrimEnd('/') + "/api/hosts", ct);
            var remote = JsonSerializer.Deserialize<List<HostView>>(json, JsonOpts) ?? [];
            views.AddRange(remote.Select(r => r with { Name = p.Name, Local = false }));
        }
        catch (Exception ex)
        {
            views.Add(new HostView(p.Name, false, ex.GetType().Name + ": " + ex.Message, new HostUsage(0, 0, 0, 0, 0, "none"), DateTime.MinValue, [], new History([], [], [])));
        }
    }
    return Results.Ok(views);
});

app.MapPost("/api/hosts/{host}/apps/{name}/{action}", async (string host, string name, string action, HttpRequest req, DockerClient docker, Sampler sampler, IHttpClientFactory f, CancellationToken ct) =>
{
    var scope = PinScope(req);
    if (scope is null) return Results.StatusCode(401);
    if (scope.Length > 0 && !scope.Contains(name, StringComparer.Ordinal))
        return Results.Problem($"Din PIN må kun styre: {string.Join(", ", scope)}.");
    if (action is not ("start" or "stop" or "restart")) return Results.BadRequest();

    // "*" = "whichever host you are" - used when a peer forwards to us.
    if (host != hostName && host != "*")
    {
        var p = peers.FirstOrDefault(x => x.Name == host);
        if (p is null) return Results.NotFound();
        using var msg = new HttpRequestMessage(HttpMethod.Post, $"{p.Url.TrimEnd('/')}/api/hosts/{Uri.EscapeDataString(p.RemoteHostName ?? "")}/apps/{name}/{action}");
        msg.Headers.Add("X-Kontrol-Pin", pin);
        var r = await f.CreateClient("peer").SendAsync(msg, ct);
        return Results.StatusCode((int)r.StatusCode);
    }

    if (action != "start" && protectedNames.Contains(name, StringComparer.Ordinal))
        return Results.Problem($"{name} er beskyttet og kan ikke slukkes herfra.");

    var ok = action switch
    {
        "start"   => await docker.StartAsync(name, ct),
        "stop"    => await docker.StopAsync(name, ct),
        "restart" => await docker.RestartAsync(name, ct),
        _         => false
    };
    if (!ok) return Results.Problem($"Docker refused to {action} {name}.");
    await Task.Delay(500, ct);
    return Results.Ok();
});

// "🏠 På hjemmesiden": which apps itmartin.dk/mine-apps shows. Kept in the Hjem app; reached over
// martinnet with the shared key Hjem__KontrolKey (magic.env). Changing needs the admin PIN.
var hjemUrl = (cfg["Kontrol:HjemUrl"] ?? "http://hjem-web:8080").TrimEnd('/');
HttpRequestMessage HjemReq(HttpMethod m, string path)
{
    var r = new HttpRequestMessage(m, hjemUrl + path);
    r.Headers.Add("X-Hjem-Key", cfg["Hjem:KontrolKey"] ?? "");
    return r;
}
app.MapGet("/api/homepage", async (IHttpClientFactory f, CancellationToken ct) =>
{
    try
    {
        using var res = await f.CreateClient("peer").SendAsync(HjemReq(HttpMethod.Get, "/api/kontrol/apps"), ct);
        return res.IsSuccessStatusCode
            ? Results.Content(await res.Content.ReadAsStringAsync(ct), "application/json")
            : Results.StatusCode((int)res.StatusCode);
    }
    catch (Exception) { return Results.StatusCode(502); }
});
app.MapPost("/api/homepage/{id:int}/awake/{awake:bool}", async (int id, bool awake, HttpRequest req, IHttpClientFactory f, CancellationToken ct) =>
{
    if (!PinOk(req)) return Results.StatusCode(401);
    try
    {
        using var res = await f.CreateClient("peer").SendAsync(HjemReq(HttpMethod.Post, $"/api/kontrol/apps/{id}/awake/{awake.ToString().ToLowerInvariant()}"), ct);
        return Results.StatusCode((int)res.StatusCode);
    }
    catch (Exception) { return Results.StatusCode(502); }
});
app.MapPost("/api/homepage/{id:int}/{show:bool}", async (int id, bool show, HttpRequest req, IHttpClientFactory f, CancellationToken ct) =>
{
    if (!PinOk(req)) return Results.StatusCode(401);
    try
    {
        using var res = await f.CreateClient("peer").SendAsync(HjemReq(HttpMethod.Post, $"/api/kontrol/apps/{id}/show/{show.ToString().ToLowerInvariant()}"), ct);
        return Results.StatusCode((int)res.StatusCode);
    }
    catch (Exception) { return Results.StatusCode(502); }
});

// Weekly cross-backup (weekly-backup.sh on the NAS writes Backups/status.json).
// /volume1 is mounted read-only at /host/disk, so the file is read straight from there.
app.MapGet("/api/backup", () => Results.Ok(BackupStatus.Read(cfg)));

// What each container is, in words (wwwroot/apps.json), plus the user's own edits from the
// ✏️ button, kept in DataDir so a redeploy never loses them. Edits win per field.
var catalogFile = Path.Combine(cfg["Kontrol:DataDir"] ?? "/data", "apps-overrides.json");
var catalogLock = new object();
Dictionary<string, Dictionary<string, string>> ReadOverrides()
{
    try
    {
        return File.Exists(catalogFile)
            ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(catalogFile)) ?? new()
            : new();
    }
    catch (JsonException) { return new(); }
}
app.MapGet("/api/catalog", () =>
{
    var overrides = ReadOverrides();
    return Results.Ok(new { overrides });
});
app.MapPut("/api/catalog/{name}", (string name, Dictionary<string, string> fields, HttpRequest req) =>
{
    if (!PinOk(req)) return Results.StatusCode(401);
    string[] allowed = ["title", "desc", "url", "off", "icon"];
    var clean = fields.Where(kv => allowed.Contains(kv.Key))
        .ToDictionary(kv => kv.Key, kv => (kv.Value ?? "").Trim()[..Math.Min((kv.Value ?? "").Trim().Length, 300)]);
    lock (catalogLock)
    {
        var all = ReadOverrides();
        all[name] = clean;
        Directory.CreateDirectory(Path.GetDirectoryName(catalogFile)!);
        File.WriteAllText(catalogFile, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }
    return Results.Ok();
});

// Kolibri product overview: every app that speaks the Kolibri /health contract
// (see ITMartin.Shared.UI MapKolibri) is polled through its public URL, so the
// whole chain - tunnel, container, db - is what gets the green dot.
// Kontrol__Kolibri__0__Name / __Url in compose.
var kolibriApps = cfg.GetSection("Kontrol:Kolibri").Get<List<KolibriApp>>() ?? [];
var kolibriCache = new KolibriCache();

app.MapGet("/api/kolibri", async (IHttpClientFactory f, CancellationToken ct) =>
{
    if (DateTime.UtcNow - kolibriCache.At < TimeSpan.FromSeconds(20) && kolibriCache.Items.Count > 0)
        return Results.Ok(kolibriCache.Items);
    var client = f.CreateClient("peer");
    var tasks = kolibriApps.Select(async a =>
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var r = await client.GetAsync(a.Url.TrimEnd('/') + "/health", ct);
            var body = await r.Content.ReadAsStringAsync(ct);
            string? version = null, kolibri = null, status = null, tenant = null; int? uptime = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    version = root.TryGetProperty("version", out var v) ? v.GetString() : null;
                    kolibri = root.TryGetProperty("kolibri", out var k) ? k.GetString() : null;
                    status  = root.TryGetProperty("status", out var st) ? st.GetString() : null;
                    tenant  = root.TryGetProperty("tenant", out var t) ? t.GetString() : null;
                    uptime  = root.TryGetProperty("uptimeSeconds", out var u) && u.TryGetInt32(out var ui) ? ui : null;
                }
            }
            catch { /* plain "ok" bodies (older apps) are fine */ }
            var ok = r.IsSuccessStatusCode && status != "fail";
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No /health yet (Kolibri not rolled out) - the app is still up if its front page answers.
                try { using var front = await client.GetAsync(a.Url, ct); ok = front.IsSuccessStatusCode || (int)front.StatusCode is 302 or 401; }
                catch { ok = false; }
                return new KolibriView(a.Name, a.Url, ok, 404, ok ? "ok" : "fail", null, null, null, null, (int)sw.ElapsedMilliseconds, ok ? null : "front page down", false);
            }
            return new KolibriView(a.Name, a.Url, ok, (int)r.StatusCode, status ?? (r.IsSuccessStatusCode ? "ok" : "fail"),
                version, kolibri, tenant, uptime, (int)sw.ElapsedMilliseconds, null, version is not null);
        }
        catch (Exception ex)
        {
            return new KolibriView(a.Name, a.Url, false, 0, "down", null, null, null, null, (int)sw.ElapsedMilliseconds, ex.GetType().Name, false);
        }
    });
    var items = (await Task.WhenAll(tasks)).ToList();
    kolibriCache.Items = items; kolibriCache.At = DateTime.UtcNow;
    return Results.Ok(items);
});

// Push alarms to Martin's phone. Subscribing needs the PIN; the browser side is wwwroot/push.js + sw.js.
app.MapGet("/api/push/public-key", (PushStore push) => Results.Text(push.PublicKey));
app.MapPost("/api/push/subscribe", (PushSubscribeRequest req, HttpRequest http, PushStore push) =>
{
    if (!PinOk(http)) return Results.StatusCode(401);
    push.Add(new PushStore.Subscriber(req.Endpoint, req.P256dh, req.Auth, req.Name ?? "telefon", DateTime.UtcNow));
    return Results.Ok(new { count = push.Count });
});
app.MapPost("/api/push/unsubscribe", (PushEndpointRequest req, PushStore push) => { push.Remove(req.Endpoint); return Results.Ok(); });
app.MapPost("/api/push/test", async (HttpRequest http, PushStore push, CancellationToken ct) =>
{
    if (!PinOk(http)) return Results.StatusCode(401);
    var n = await push.SendAllAsync("Kontrol: test", "Alarmer virker på denne telefon.", "/", ct);
    return Results.Ok(new { sent = n });
});
app.MapGet("/api/alarms", (AlarmService alarms, PushStore push) => Results.Ok(new { subscribers = push.Count, active = alarms.Active.Select(a => new { key = a.Key, since = a.Value }) }));

app.Run();

public sealed record KolibriApp(string Name, string Url);
public sealed record KolibriView(string Name, string Url, bool Ok, int Http, string Status, string? Version, string? Kolibri, string? Tenant, int? UptimeSeconds, int LatencyMs, string? Error, bool SpeaksKolibri);
public sealed class KolibriCache { public List<KolibriView> Items { get; set; } = []; public DateTime At { get; set; } }
public sealed record PushSubscribeRequest(string Endpoint, string P256dh, string Auth, string? Name);
public sealed record PushEndpointRequest(string Endpoint);
public sealed record LimitedPin(string Pin, string[] Apps);
public sealed record Peer(string Name, string Url, string? RemoteHostName = "*");
public sealed record ContainerView(string Name, string Status, bool Running, string Image, string Ports, bool Protected, double? CpuPct, double? MemMb, double[] CpuHistory);
public sealed record History(DateTime[] T, double[] Cpu, double[] Mem);
public sealed record HostView(string Name, bool Local, string? Error, HostUsage Host, DateTime At, List<ContainerView> Containers, History History);

public sealed record BackupView(bool Found, string? LastRun, bool Ok, double AgeDays, bool Stale, string? Error, string? NasToPhotoserver, string? PhotoserverToNas);
public static class BackupStatus
{
    public static BackupView Read(IConfiguration cfg)
    {
        var path = cfg["Kontrol:BackupStatus"] ?? "/host/disk/homes/MartinHvidberg/Backups/status.json";
        var maxAge = cfg.GetValue("Kontrol:BackupMaxAgeDays", 8);
        try
        {
            if (!File.Exists(path)) return new(false, null, false, 0, true, "status.json findes ikke", null, null);
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var r = doc.RootElement;
            var last = r.TryGetProperty("lastRun", out var l) ? l.GetString() : null;
            var ok = r.TryGetProperty("ok", out var o) && o.ValueKind == System.Text.Json.JsonValueKind.True;
            var age = DateOnly.TryParse(last, out var d) ? (DateTime.UtcNow.Date - d.ToDateTime(TimeOnly.MinValue)).TotalDays : 999;
            string? Sum(string key) => r.TryGetProperty(key, out var x) && x.ValueKind == System.Text.Json.JsonValueKind.Object
                ? $"{(x.TryGetProperty("files", out var f) ? f.ToString() : "?")} filer, {(x.TryGetProperty("size", out var sz) ? sz.GetString() : "?")}" : null;
            return new(true, last, ok, age, !ok || age > maxAge, r.TryGetProperty("error", out var e) ? e.GetString() : null, Sum("nasToPhotoserver"), Sum("photoserverToNas"));
        }
        catch (Exception ex) { return new(false, null, false, 0, true, ex.Message, null, null); }
    }
}
