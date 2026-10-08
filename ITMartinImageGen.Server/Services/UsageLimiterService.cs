using System.Collections.Concurrent;
using System.Text.Json;

namespace ITMartinImageGen.Server.Services;

/// <summary>
/// Cost guardrail for a public, unauthenticated app where every click fires a real paid API
/// call. Two independent limits:
///   - A global daily cap (persisted to disk so a container restart can't reset it early).
///   - A per-visitor hourly cap (in-memory sliding window, keyed by IP).
/// Neither limit is about being clever — they exist purely so a spam-click burst (accidental
/// or deliberate) can't run up an unbounded bill while nobody's watching.
/// </summary>
public sealed class UsageLimiterService
{
    private const int DailyGlobalCap      = 150;
    private const int PerVisitorHourlyCap = 8;
    private static readonly TimeSpan HourWindow = TimeSpan.FromHours(1);

    private readonly string _stateFile;
    private readonly object _dailyLock = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _visitorHits = new();

    private int _dailyCount;
    private DateOnly _dailyDate;

    // The free version (user 2026-10-08: "watch out for payment"): text -> image only, a few per visitor per DAY,
    // and its own small daily ceiling so free use can never eat the whole budget. Both from config.
    private readonly int _freeDailyCap;
    private readonly int _freePerVisitorDaily;
    private int _freeCount;
    private readonly ConcurrentDictionary<string, int> _freeVisitor = new();

    public int FreePerVisitorDaily => _freePerVisitorDaily;

    public UsageLimiterService(IConfiguration config)
    {
        _freeDailyCap = config.GetValue("ImageGen:FreeDailyCap", 30);
        _freePerVisitorDaily = config.GetValue("ImageGen:FreePerVisitorDaily", 5);
        var imagesRoot = config["ImageStorage:Root"] ?? "/app/data/images";
        var dataDir = Path.GetDirectoryName(imagesRoot) ?? "/app/data";
        Directory.CreateDirectory(dataDir);
        _stateFile = Path.Combine(dataDir, "usage-limiter.json");
        LoadDailyState();
    }

    public (bool Allowed, string? DenyReasonDanish) TryConsume(string visitorKey)
    {
        var now = DateTime.UtcNow;

        lock (_dailyLock)
        {
            var today = DateOnly.FromDateTime(now);
            if (today != _dailyDate) { _dailyDate = today; _dailyCount = 0; _freeCount = 0; _freeVisitor.Clear(); }

            if (_dailyCount >= DailyGlobalCap)
                return (false, "Det daglige loft for billed-generering er nået for i dag. Prøv igen i morgen.");
        }

        var queue = _visitorHits.GetOrAdd(visitorKey, _ => new Queue<DateTime>());
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > HourWindow)
                queue.Dequeue();

            if (queue.Count >= PerVisitorHourlyCap)
                return (false, $"Du har nået grænsen på {PerVisitorHourlyCap} billeder i timen. Prøv igen om lidt.");

            queue.Enqueue(now);
        }

        lock (_dailyLock)
        {
            _dailyCount++;
            SaveDailyState();
        }

        return (true, null);
    }

    /// <summary>Free images left today for this visitor (for the "3 af 5 tilbage i dag" line).</summary>
    public int FreeLeft(string visitorKey)
    {
        lock (_dailyLock)
        {
            RollDay();
            return Math.Max(0, _freePerVisitorDaily - _freeVisitor.GetValueOrDefault(visitorKey));
        }
    }

    public (bool Allowed, string? DenyReasonDanish) TryConsumeFree(string visitorKey)
    {
        lock (_dailyLock)
        {
            RollDay();
            if (_dailyCount >= DailyGlobalCap || _freeCount >= _freeDailyCap)
                return (false, "Dagens gratis billeder er brugt op. Prøv igen i morgen.");
            var used = _freeVisitor.GetValueOrDefault(visitorKey);
            if (used >= _freePerVisitorDaily)
                return (false, $"Du har lavet dine {_freePerVisitorDaily} gratis billeder i dag. Prøv igen i morgen – eller spørg om den udvidede udgave.");
            _freeVisitor[visitorKey] = used + 1;
            _freeCount++;
            _dailyCount++;
            SaveDailyState();
        }
        return (true, null);
    }

    private void RollDay()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today == _dailyDate) return;
        _dailyDate = today; _dailyCount = 0; _freeCount = 0; _freeVisitor.Clear();
    }

    private void LoadDailyState()
    {
        lock (_dailyLock)
        {
            _dailyDate = DateOnly.FromDateTime(DateTime.UtcNow);
            _dailyCount = 0;
            if (!File.Exists(_stateFile)) return;
            try
            {
                var saved = JsonSerializer.Deserialize<DailyState>(File.ReadAllText(_stateFile));
                if (saved is not null && saved.Date == _dailyDate)
                {
                    _dailyCount = saved.Count;
                    _freeCount = saved.FreeCount;
                    foreach (var (k, v) in saved.FreeVisitors ?? []) _freeVisitor[k] = v;
                }
            }
            catch { /* corrupt file — start the day fresh rather than fail startup */ }
        }
    }

    private void SaveDailyState()
    {
        try { File.WriteAllText(_stateFile, JsonSerializer.Serialize(new DailyState(_dailyDate, _dailyCount, _freeCount, new Dictionary<string, int>(_freeVisitor)))); }
        catch { /* best-effort persistence — a failed write just means a restart could reset the count early */ }
    }

    private sealed record DailyState(DateOnly Date, int Count, int FreeCount = 0, Dictionary<string, int>? FreeVisitors = null);
}
