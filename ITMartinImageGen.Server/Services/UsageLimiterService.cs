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

    public UsageLimiterService(IConfiguration config)
    {
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
            if (today != _dailyDate) { _dailyDate = today; _dailyCount = 0; }

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
                    _dailyCount = saved.Count;
            }
            catch { /* corrupt file — start the day fresh rather than fail startup */ }
        }
    }

    private void SaveDailyState()
    {
        try { File.WriteAllText(_stateFile, JsonSerializer.Serialize(new DailyState(_dailyDate, _dailyCount))); }
        catch { /* best-effort persistence — a failed write just means a restart could reset the count early */ }
    }

    private sealed record DailyState(DateOnly Date, int Count);
}
