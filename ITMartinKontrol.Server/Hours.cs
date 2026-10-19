using System.Text.Json;
using ITMartinKontrol.Domain;

namespace ITMartinKontrol.Server;

/// <summary>Opening hours per container ("07-17"), kept in Kontrol:DataDir next to the priorities.</summary>
public sealed class HoursStore
{
    private readonly string _file;
    private readonly object _gate = new();
    private Dictionary<string, string> _map = new();

    public HoursStore(IConfiguration cfg)
    {
        _file = Path.Combine(cfg["Kontrol:DataDir"] ?? "/data", "hours.json");
        try { if (File.Exists(_file)) _map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_file)) ?? new(); }
        catch (JsonException) { }
    }

    public OpeningHours? Get(string name) { lock (_gate) return _map.TryGetValue(name, out var h) ? OpeningHours.Parse(h) : null; }

    public Dictionary<string, OpeningHours> All()
    {
        lock (_gate) return _map.Select(kv => (kv.Key, H: OpeningHours.Parse(kv.Value))).Where(x => x.H is not null).ToDictionary(x => x.Key, x => x.H!);
    }

    /// <summary>null = always on (no hours).</summary>
    public void Set(string name, OpeningHours? hours)
    {
        lock (_gate)
        {
            if (hours is null) _map.Remove(name); else _map[name] = hours.ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}

/// <summary>
/// Every minute: starts an app when its opening hours begin and stops it when they end (HoursPolicy). A start waits
/// while memory is low (same rule as "Genstart i rækkefølge") and is tried again each minute until the hours end.
/// This host only - the photoserver's Kontrol keeps the photoserver's hours.
/// </summary>
public sealed class HoursKeeper(DockerClient docker, HoursStore hours, PriorityStore prio, MemoryGuard guard, IConfiguration cfg, ILogger<HoursKeeper> log) : BackgroundService
{
    private static readonly TimeZoneInfo Danish = FindDanish();
    private readonly GuardLimits _limits = new(cfg.GetValue("Kontrol:Guard:LowMb", 250), cfg.GetValue("Kontrol:Guard:CriticalMb", 120), cfg.GetValue("Kontrol:Guard:SwapInPerSec", 400));
    private readonly Dictionary<string, bool> _wasOpen = new();
    private readonly HashSet<string> _waitingToStart = [];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        while (!ct.IsCancellationRequested)
        {
            try { await CheckAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { log.LogWarning(ex, "Opening hours check failed"); }
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        var now = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, Danish));
        var all = hours.All();
        if (all.Count == 0) return;
        var running = (await docker.ListAllAsync(ct)).ToDictionary(c => c.Name, c => c.Running);
        foreach (var (name, h) in all)
        {
            if (!running.TryGetValue(name, out var isRunning)) continue;   // not on this host
            var open = h.IsOpenAt(now);
            var step = HoursPolicy.Decide(_wasOpen.TryGetValue(name, out var w) ? w : null, open, isRunning);
            _wasOpen[name] = open;
            if (!open) _waitingToStart.Remove(name);
            if (step == HoursStep.Stop)
            {
                await docker.StopAsync(name, ct);
                log.LogInformation("Opening hours {Hours}: stopped {Name}", h, name);
                guard.Note(name, $"slukket - åbningstid {h}");
            }
            else if (step == HoursStep.Start) _waitingToStart.Add(name);

            if (open && !isRunning && _waitingToStart.Contains(name))
            {
                if (!RestartPlan.MayStart(prio.Get(name), guard.Status.Last?.AvailMb ?? 0, _limits)) continue;   // try again next minute
                _waitingToStart.Remove(name);
                await docker.StartAsync(name, ct);
                log.LogInformation("Opening hours {Hours}: started {Name}", h, name);
                guard.Note(name, $"tændt - åbningstid {h}");
            }
        }
    }

    private static TimeZoneInfo FindDanish()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"); }
        catch (Exception) { return TimeZoneInfo.Local; }
    }
}
