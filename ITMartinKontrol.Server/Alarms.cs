using System.Text.Json;
using WebPush;

namespace ITMartinKontrol.Server;

/// <summary>
/// Web-push to Martin's phone. Subscriptions and the VAPID key pair live in
/// Kontrol:DataDir so a redeploy keeps them. Same WebPush pattern as MinElpris.
/// </summary>
public sealed class PushStore
{
    private readonly string _keysFile, _subsFile;
    private readonly object _gate = new();
    private readonly VapidDetails _vapid;
    private readonly WebPushClient _client = new();
    private readonly ILogger<PushStore> _log;
    private List<Subscriber> _subs = [];

    public sealed record Subscriber(string Endpoint, string P256dh, string Auth, string Name, DateTime AddedAt);
    private sealed record VapidKeys(string PublicKey, string PrivateKey);

    public PushStore(IConfiguration cfg, ILogger<PushStore> log)
    {
        _log = log;
        var dir = cfg["Kontrol:DataDir"] ?? "/data";
        Directory.CreateDirectory(dir);
        _keysFile = Path.Combine(dir, "vapid.json");
        _subsFile = Path.Combine(dir, "push-subscribers.json");
        VapidKeys k;
        if (File.Exists(_keysFile)) k = JsonSerializer.Deserialize<VapidKeys>(File.ReadAllText(_keysFile))!;
        else
        {
            var g = VapidHelper.GenerateVapidKeys();
            k = new VapidKeys(g.PublicKey, g.PrivateKey);
            File.WriteAllText(_keysFile, JsonSerializer.Serialize(k));
        }
        _vapid = new VapidDetails("mailto:ITMartin@Mensa.dk", k.PublicKey, k.PrivateKey);
        if (File.Exists(_subsFile))
            try { _subs = JsonSerializer.Deserialize<List<Subscriber>>(File.ReadAllText(_subsFile)) ?? []; } catch { }
    }

    public string PublicKey => _vapid.PublicKey;
    public int Count { get { lock (_gate) return _subs.Count; } }

    public void Add(Subscriber s)
    {
        lock (_gate) { _subs.RemoveAll(x => x.Endpoint == s.Endpoint); _subs.Add(s); Save(); }
    }

    public void Remove(string endpoint)
    {
        lock (_gate) { _subs.RemoveAll(x => x.Endpoint == endpoint); Save(); }
    }

    private void Save() => File.WriteAllText(_subsFile, JsonSerializer.Serialize(_subs));

    /// <summary>Sends to every phone; drops endpoints that are gone. Returns how many got it.</summary>
    public async Task<int> SendAllAsync(string title, string body, string url = "/", CancellationToken ct = default)
    {
        List<Subscriber> targets; lock (_gate) targets = _subs.ToList();
        var payload = JsonSerializer.Serialize(new { title, body, url });
        var sent = 0;
        foreach (var s in targets)
        {
            try
            {
                await _client.SendNotificationAsync(new PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, _vapid, ct);
                sent++;
            }
            catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
            {
                _log.LogInformation("Push subscriber {Name} is gone, removing", s.Name);
                Remove(s.Endpoint);
            }
            catch (Exception ex) { _log.LogWarning(ex, "Push to {Name} failed", s.Name); }
        }
        return sent;
    }
}

/// <summary>
/// The alarm rules from the plan (tab 12 Svæv): container down, CPU/RAM/disk
/// over the line, peer unreachable, Kolibri app red. One push per condition,
/// then silence until it recovers (a recovery push closes it), so a bad night
/// is two messages, not two hundred.
/// </summary>
public sealed class AlarmService(DockerClient docker, Sampler sampler, PushStore push, IHttpClientFactory f, IConfiguration cfg, ILogger<AlarmService> log) : BackgroundService
{
    private readonly Dictionary<string, DateTime> _active = new();          // condition key -> first seen
    private readonly Dictionary<string, DateTime> _firstSeen = new();       // for "sustained" rules
    private readonly HashSet<string> _wasRunning = [];
    private readonly double _cpuPct = cfg.GetValue("Kontrol:Alarm:CpuPct", 80.0);
    private readonly double _memPct = cfg.GetValue("Kontrol:Alarm:MemPct", 90.0);
    private readonly double _diskPct = cfg.GetValue("Kontrol:Alarm:DiskPct", 85.0);
    private readonly int _sustainMin = cfg.GetValue("Kontrol:Alarm:SustainMinutes", 15);
    private readonly string _host = cfg["Kontrol:HostName"] ?? Environment.MachineName;
    private readonly List<Peer> _peers = cfg.GetSection("Kontrol:Peers").Get<List<Peer>>() ?? [];
    private readonly List<KolibriApp> _apps = cfg.GetSection("Kontrol:Kolibri").Get<List<KolibriApp>>() ?? [];

    public IReadOnlyDictionary<string, DateTime> Active { get { lock (_active) return new Dictionary<string, DateTime>(_active); } }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(90), ct); // let the sampler take its first samples
        while (!ct.IsCancellationRequested)
        {
            try { await CheckAsync(ct); }
            catch (Exception ex) { log.LogWarning(ex, "Alarm check failed"); }
            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var seen = new HashSet<string>();

        // 1. Host resources (sustained for CPU/RAM, immediate for disk).
        var h = sampler.LastHost;
        if (h.MemTotalMb > 0)
        {
            Sustained($"cpu:{_host}", h.CpuPct >= _cpuPct, now, seen, $"CPU {h.CpuPct:0} % i {_sustainMin} min på {_host}");
            Sustained($"mem:{_host}", h.MemUsedMb / h.MemTotalMb * 100 >= _memPct, now, seen, $"RAM {h.MemUsedMb / h.MemTotalMb * 100:0} % i {_sustainMin} min på {_host}");
        }
        if (h.DiskTotalGb > 0)
            Immediate($"disk:{_host}", h.DiskUsedGb / h.DiskTotalGb * 100 >= _diskPct, seen, $"Disk {h.DiskUsedGb / h.DiskTotalGb * 100:0} % fuld på {_host}");

        // 2. Containers that were running and are not any more. Sustained, not immediate: a
        // deploy recreates the container and it is gone for seconds - that sent a red and a
        // green push per app on every deploy (2026-09-28). A real stop still alarms after 3 min.
        var containers = await docker.ListAllAsync(ct);
        var stopped = new HashSet<string>(containers.Where(c => !c.Running).Select(c => c.Name), StringComparer.Ordinal);
        foreach (var c in containers)
        {
            var key = $"down:{_host}:{c.Name}";
            if (c.Running) { _wasRunning.Add(c.Name); Sustained(key, false, now, seen, ""); }
            else if (_wasRunning.Contains(c.Name)) Sustained(key, true, now, seen, $"{Title(c.Name)} er stoppet på {_host}", sustainMinutes: 3);
        }

        // 3. Peers. "Up" = its container list answers. Its /health also counts the backup
        // check, which only the NAS can see, so the photoserver always looked down.
        foreach (var p in _peers)
        {
            bool up;
            try
            {
                using var r = await f.CreateClient("peer").GetAsync(p.Url.TrimEnd('/') + "/api/hosts", ct);
                up = r.IsSuccessStatusCode;
                if (up)
                {
                    var remote = JsonSerializer.Deserialize<List<HostView>>(await r.Content.ReadAsStringAsync(ct), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
                    foreach (var c in remote.SelectMany(x => x.Containers).Where(c => !c.Running)) stopped.Add(c.Name);
                }
            }
            catch { up = false; }
            Sustained($"peer:{p.Name}", !up, now, seen, $"{p.Name} svarer ikke", sustainMinutes: 10);
        }

        // 4. Kolibri apps (public URL, whole chain). An app whose container was switched off
        // on purpose (Stem, Forløbet ...) is not "down" - no alarm for that.
        foreach (var a in _apps)
        {
            if (ContainerFor(a.Url) is { } cn && stopped.Contains(cn)) continue;
            bool ok;
            try
            {
                using var r = await f.CreateClient("peer").GetAsync(a.Url.TrimEnd('/') + "/health", ct);
                ok = r.IsSuccessStatusCode || (int)r.StatusCode == 404; // 404 = no /health yet, not down
            }
            catch { ok = false; }
            Sustained($"app:{a.Name}", !ok, now, seen, $"{a.Name} er nede ({a.Url})", sustainMinutes: 3);
        }

        // 5. Weekly backup: missing, failed or older than the allowed age.
        var b = BackupStatus.Read(cfg);
        Immediate("backup:weekly", b.Stale, seen, b.Found ? (b.Ok ? $"Backup er {b.AgeDays:0} dage gammel (sidst {b.LastRun})" : $"Backup fejlede {b.LastRun}: {b.Error}") : "Backup-status mangler");

        // Anything active that was not seen this round has recovered.
        List<string> recovered;
        lock (_active) recovered = _active.Keys.Where(k => !seen.Contains(k)).ToList();
        foreach (var k in recovered)
        {
            lock (_active) _active.Remove(k);
            await push.SendAllAsync("Kontrol: ok igen", Pretty(k), "/", ct);
        }
    }

    // Friendly names and addresses from wwwroot/apps.json, so a push says "Galleri er stoppet"
    // instead of "gallery-web er stoppet".
    private static readonly Dictionary<string, (string Title, string Url)> Catalog = LoadCatalog();
    private static Dictionary<string, (string Title, string Url)> LoadCatalog()
    {
        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, "wwwroot", "apps.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Object)
                .ToDictionary(p => p.Name, p => (
                    p.Value.TryGetProperty("title", out var t) ? t.GetString() ?? p.Name : p.Name,
                    p.Value.TryGetProperty("url", out var u) ? u.GetString() ?? "" : ""));
        }
        catch { return new(); }
    }
    private static string Title(string container) => Catalog.TryGetValue(container, out var c) ? c.Title : container;
    private static string? ContainerFor(string url)
    {
        var host = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;
        return Catalog.FirstOrDefault(kv => kv.Value.Url.Length > 0 && Uri.TryCreate(kv.Value.Url, UriKind.Absolute, out var cu) && cu.Host == host).Key;
    }

    private static string Pretty(string key) => key.Split(':', 2) switch
    {
        ["cpu", var h] => $"CPU normal på {h}",
        ["mem", var h] => $"RAM normal på {h}",
        ["disk", var h] => $"Disk under grænsen på {h}",
        ["down", var rest] => $"{Title(rest.Split(':').Last())} kører igen",
        ["peer", var p] => $"{p} svarer igen",
        ["backup", _] => "Backup kørte igen",
        ["app", var a] => $"{a} svarer igen",
        _ => key
    };

    private void Immediate(string key, bool condition, HashSet<string> seen, string message)
    {
        if (!condition) return;
        seen.Add(key);
        Raise(key, message);
    }

    private void Sustained(string key, bool condition, DateTime now, HashSet<string> seen, string message, int? sustainMinutes = null)
    {
        if (!condition) { _firstSeen.Remove(key); return; }
        if (!_firstSeen.TryGetValue(key, out var first)) { _firstSeen[key] = now; first = now; }
        var need = TimeSpan.FromMinutes(sustainMinutes ?? _sustainMin);
        lock (_active) if (_active.ContainsKey(key)) { seen.Add(key); return; }
        if (now - first < need) return;
        seen.Add(key);
        Raise(key, message);
    }

    private void Raise(string key, string message)
    {
        lock (_active)
        {
            if (_active.ContainsKey(key)) return;
            _active[key] = DateTime.UtcNow;
        }
        log.LogWarning("KONTROL-ALARM {Key}: {Message}", key, message);
        _ = push.SendAllAsync("Kontrol: rødt", message, "/");
    }
}
