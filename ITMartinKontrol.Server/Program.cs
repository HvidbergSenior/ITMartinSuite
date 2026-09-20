using System.Text.Json;
using ITMartinKontrol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DockerClient>();
builder.Services.AddSingleton<Sampler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Sampler>());
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

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

public sealed record Peer(string Name, string Url, string? RemoteHostName = "*");
public sealed record ContainerView(string Name, string Status, bool Running, string Image, string Ports, bool Protected, double? CpuPct, double? MemMb, double[] CpuHistory);
public sealed record History(DateTime[] T, double[] Cpu, double[] Mem);
public sealed record HostView(string Name, bool Local, string? Error, HostUsage Host, DateTime At, List<ContainerView> Containers, History History);
