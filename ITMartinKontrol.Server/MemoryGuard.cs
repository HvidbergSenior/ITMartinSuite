using System.Text.Json;
using ITMartinKontrol.Domain;

namespace ITMartinKontrol.Server;

/// <summary>Martin's priority per container, kept in Kontrol:DataDir so a redeploy keeps it.</summary>
public sealed class PriorityStore
{
    private readonly string _file;
    private readonly object _gate = new();
    private Dictionary<string, int> _map = new();

    public PriorityStore(IConfiguration cfg)
    {
        _file = Path.Combine(cfg["Kontrol:DataDir"] ?? "/data", "priorities.json");
        try { if (File.Exists(_file)) _map = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(_file)) ?? new(); }
        catch (JsonException) { }
    }

    public int Get(string name) { lock (_gate) return _map.TryGetValue(name, out var p) ? p : Priority.Important; }

    public void Set(string name, int level)
    {
        lock (_gate)
        {
            _map[name] = level;
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}

public sealed record MemReading(double AvailMb, double SwapUsedMb, double SwapInPerSec);
public sealed record GuardAction(DateTime At, string Name, string What);
public sealed record GuardStatus(bool Enabled, int LowMb, int CriticalMb, int SwapInPerSec, MemReading? Last, bool Resetting, string? ResetStep, List<GuardAction> Actions);

/// <summary>
/// Every 15 s: free RAM + swap-in rate from the host's /proc. When memory stays low it stops one
/// app at a time by priority (GuardPolicy), with a cooldown so each stop can free its memory, and
/// it stops any container Docker keeps restarting. Also runs "Genstart i rækkefølge" (RestartPlan).
/// Each automatic stop = a push. This host only; the photoserver's Kontrol guards the photoserver.
/// </summary>
public sealed class MemoryGuard(DockerClient docker, Sampler sampler, PriorityStore prio, PushStore push, IConfiguration cfg, ILogger<MemoryGuard> log) : BackgroundService
{
    private readonly bool _enabled = cfg.GetValue("Kontrol:Guard:Enabled", true);
    private readonly GuardLimits _limits = new(
        cfg.GetValue("Kontrol:Guard:LowMb", 250),
        cfg.GetValue("Kontrol:Guard:CriticalMb", 120),
        cfg.GetValue("Kontrol:Guard:SwapInPerSec", 400));
    private readonly int _checkSec = cfg.GetValue("Kontrol:Guard:CheckSeconds", 15);
    private readonly int _cooldownSec = cfg.GetValue("Kontrol:Guard:CooldownSeconds", 60);
    private readonly int _loopChecks = cfg.GetValue("Kontrol:Guard:LoopChecks", 3);
    private readonly string _proc = cfg["Kontrol:HostProc"] ?? "/host/proc";
    private readonly string _host = cfg["Kontrol:HostName"] ?? Environment.MachineName;
    private readonly string[] _protected = cfg.GetSection("Kontrol:Protected").Get<string[]>() ?? ["kontrol-web", "cloudflared"];

    private readonly Dictionary<string, int> _restarting = new();
    private readonly List<GuardAction> _actions = [];
    private (double pages, DateTime at)? _lastSwapIn;
    private MemReading? _last;
    private int _lowStreak;
    private DateTime _lastStop = DateTime.MinValue;
    private bool _warnedNothingToStop;
    private volatile bool _resetting;
    private volatile string? _resetStep;

    public GuardStatus Status { get { lock (_actions) return new(_enabled, _limits.LowMb, _limits.CriticalMb, _limits.SwapInPerSec, _last, _resetting, _resetStep, _actions.ToList()); } }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_enabled) { log.LogInformation("Memory guard is off (Kontrol__Guard__Enabled=false)."); return; }
        await Task.Delay(TimeSpan.FromSeconds(30), ct);
        while (!ct.IsCancellationRequested)
        {
            try { if (!_resetting) await CheckAsync(ct); else _last = ReadMem() ?? _last; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { log.LogWarning(ex, "Memory guard check failed"); }
            await Task.Delay(TimeSpan.FromSeconds(_checkSec), ct);
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        var containers = await docker.ListAllAsync(ct);

        // Restart loops: a container Docker keeps restarting serves nobody and costs a fresh .NET start each time.
        foreach (var c in containers)
        {
            if (c.State != "restarting") { _restarting.Remove(c.Name); continue; }
            var n = _restarting[c.Name] = _restarting.GetValueOrDefault(c.Name) + 1;
            if (n < _loopChecks || _protected.Contains(c.Name)) continue;
            _restarting.Remove(c.Name);
            await StopAsync(c.Name, "gik ned og genstartede igen og igen", ct, notify: true);
        }

        var m = ReadMem();
        if (m is null) return;
        _last = m;
        var p = GuardPolicy.Assess(m.AvailMb, m.SwapInPerSec, _limits);
        _lowStreak = p == Pressure.Ok ? 0 : _lowStreak + 1;
        if (p == Pressure.Ok) { _warnedNothingToStop = false; return; }
        if (p == Pressure.Low && _lowStreak < 2) return;                              // one bad reading is not a trend
        if (DateTime.UtcNow - _lastStop < TimeSpan.FromSeconds(_cooldownSec)) return; // let the last stop free its memory

        var victim = GuardPolicy.PickVictim(Candidates(containers.Where(c => c.Running)), p);
        var why = Why(m);
        if (victim is null)
        {
            if (_warnedNothingToStop) return;
            _warnedNothingToStop = true;
            log.LogWarning("Memory low on {Host} ({Why}) but nothing may be stopped", _host, why);
            await push.SendAllAsync("Kontrol: RAM lav", $"{_host}: {why}. Ingen app står til 'Kan vente' – sluk en selv i Kontrol.", "/", ct);
            return;
        }
        await StopAsync(victim, why, ct, notify: true);
        _lowStreak = 0;
    }

    /// <summary>Stops the running apps (Kan vente first), then starts them one by one by priority with time to settle.</summary>
    public bool TryStartReset()
    {
        if (_resetting) return false;
        _resetting = true;
        _ = Task.Run(async () =>
        {
            try { await ResetAsync(CancellationToken.None); }
            catch (Exception ex) { log.LogWarning(ex, "Reset failed"); Record("Genstart", "stoppede med en fejl: " + ex.Message); }
            finally { _resetting = false; _resetStep = null; }
        });
        return true;
    }

    private async Task ResetAsync(CancellationToken ct)
    {
        var running = Candidates((await docker.ListAllAsync(ct)).Where(c => c.Running)).ToList();
        var order = RestartPlan.StartOrder(running);
        Record("Genstart", $"begynder: {order.Count} apps slukkes og tændes én ad gangen");

        foreach (var name in Enumerable.Reverse(order))
        {
            _resetStep = $"Slukker {name}";
            await docker.StopAsync(name, ct);
        }
        _resetStep = "Venter på at RAM bliver fri";
        await Task.Delay(TimeSpan.FromSeconds(15), ct);

        var skipped = new List<string>();
        for (var i = 0; i < order.Count; i++)
        {
            var name = order[i];
            var level = prio.Get(name);
            _resetStep = $"Tænder {i + 1} af {order.Count}: {name} ({Priority.Label(level)})";
            // Wait up to a minute for room; a "Kan vente"/"Vigtig" app that still has no room stays off.
            var waited = 0;
            while (!RestartPlan.MayStart(level, ReadMem()?.AvailMb ?? 0, _limits) && waited < 60) { await Task.Delay(5000, ct); waited += 5; }
            if (!RestartPlan.MayStart(level, ReadMem()?.AvailMb ?? 0, _limits)) { skipped.Add(name); continue; }
            await docker.StartAsync(name, ct);
            await Task.Delay(TimeSpan.FromSeconds(20), ct); // a .NET app needs this long to start before the next one begins
        }
        var done = $"færdig: {order.Count - skipped.Count} tændt" + (skipped.Count > 0 ? $", ikke plads til {string.Join(", ", skipped)}" : "");
        Record("Genstart", done);
        await push.SendAllAsync("Kontrol: genstart " + done.Split(':')[0], $"{_host}: {done}.", "/", ct);
    }

    private IEnumerable<GuardCandidate> Candidates(IEnumerable<DockerClient.ContainerInfo> cs)
    {
        var usage = sampler.LastContainers;
        return cs.Select(c => new GuardCandidate(c.Name, prio.Get(c.Name), usage.TryGetValue(c.Name, out var u) ? u.MemMb : 0, _protected.Contains(c.Name)));
    }

    private string Why(MemReading m) =>
        $"kun {m.AvailMb:0} MB fri RAM" + (m.SwapInPerSec > _limits.SwapInPerSec ? $" og den læser {m.SwapInPerSec:0} sider/s fra swap" : "");

    private async Task StopAsync(string name, string why, CancellationToken ct, bool notify)
    {
        var ok = await docker.StopAsync(name, ct);
        _lastStop = DateTime.UtcNow;
        log.LogWarning("Memory guard stopped {Name} on {Host}: {Why} (ok={Ok})", name, _host, why, ok);
        Record(name, "slukket automatisk: " + why);
        if (notify) await push.SendAllAsync($"Kontrol slukkede {name}", $"{_host}: {why}. Tænd den igen i Kontrol, når der er plads.", "/", ct);
    }

    private void Record(string name, string what)
    {
        lock (_actions)
        {
            _actions.Insert(0, new GuardAction(DateTime.UtcNow, name, what));
            if (_actions.Count > 20) _actions.RemoveAt(20);
        }
    }

    private MemReading? ReadMem()
    {
        try
        {
            double avail = 0, swapTotal = 0, swapFree = 0;
            foreach (var l in File.ReadLines(Path.Combine(_proc, "meminfo")))
            {
                if (l.StartsWith("MemAvailable:")) avail = Kb(l);
                else if (l.StartsWith("SwapTotal:")) swapTotal = Kb(l);
                else if (l.StartsWith("SwapFree:")) swapFree = Kb(l);
            }
            var line = File.ReadLines(Path.Combine(_proc, "vmstat")).FirstOrDefault(l => l.StartsWith("pswpin "));
            var pages = line is null ? 0 : double.Parse(line.Split(' ')[1]);
            var now = DateTime.UtcNow;
            var rate = _lastSwapIn is { } p && (now - p.at).TotalSeconds > 0 ? Math.Max(0, (pages - p.pages) / (now - p.at).TotalSeconds) : 0;
            _lastSwapIn = (pages, now);
            return avail > 0 ? new MemReading(Math.Round(avail / 1024), Math.Round((swapTotal - swapFree) / 1024), Math.Round(rate)) : null;
        }
        catch { return null; }
        static double Kb(string l) => double.Parse(l.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);
    }
}
