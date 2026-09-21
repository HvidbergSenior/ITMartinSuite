using System.Text.Json;
using ITMartinKontrol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DockerClient>();
builder.Services.AddSingleton<Sampler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Sampler>());
builder.Services.AddSingleton<PushStore>();
builder.Services.AddSingleton<AlarmService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AlarmService>());
builder.Services.AddHttpClient("peer", c => c.Timeout = TimeSpan.FromSeconds(8));

var app = builder.Build();
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

bool PinOk(HttpRequest req) =>
    !string.IsNullOrWhiteSpace(pin)
    && req.Headers.TryGetValue("X-Kontrol-Pin", out var v)
    && string.Equals(v.ToString(), pin, StringComparison.Ordinal);

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
    if (!PinOk(req)) return Results.StatusCode(401);
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

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

public sealed record KolibriApp(string Name, string Url);
public sealed record KolibriView(string Name, string Url, bool Ok, int Http, string Status, string? Version, string? Kolibri, string? Tenant, int? UptimeSeconds, int LatencyMs, string? Error, bool SpeaksKolibri);
public sealed class KolibriCache { public List<KolibriView> Items { get; set; } = []; public DateTime At { get; set; } }
public sealed record PushSubscribeRequest(string Endpoint, string P256dh, string Auth, string? Name);
public sealed record PushEndpointRequest(string Endpoint);
public sealed record Peer(string Name, string Url, string? RemoteHostName = "*");
public sealed record ContainerView(string Name, string Status, bool Running, string Image, string Ports, bool Protected, double? CpuPct, double? MemMb, double[] CpuHistory);
public sealed record History(DateTime[] T, double[] Cpu, double[] Mem);
public sealed record HostView(string Name, bool Local, string? Error, HostUsage Host, DateTime At, List<ContainerView> Containers, History History);
