using ITMartinNotat;
using ITMartinNotatskriver.Application;
using Microsoft.Extensions.Logging;

namespace ITMartinNotatskriver.Infrastructure;

/// <summary>Counts AI calls per UTC day in memory. A restart resets the count - acceptable, as it only raises the
/// ceiling once; the real money cap is the per-call token limit plus this.</summary>
public sealed class DailyAiBudget(int maxPerDay, TimeProvider clock, ILogger<DailyAiBudget> logger) : IAiBudget
{
    private readonly object _lock = new();
    private DateOnly _day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private int _used;

    public int MaxPerDay => maxPerDay;

    public bool TryTake(string what)
    {
        lock (_lock)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            if (today != _day) { _day = today; _used = 0; }
            if (_used >= maxPerDay)
            {
                logger.LogWarning("AI budget spent ({Used}/{Max}) - refused {What}", _used, maxPerDay, what);
                return false;
            }
            _used++;
            return true;
        }
    }
}

/// <summary>The Windows edition's pseudonymizer behind the application's port.</summary>
public sealed class PseudonymizerHiderFactory : INameHiderFactory
{
    public INameHider Create() => new Hider(new Pseudonymizer());

    private sealed class Hider(Pseudonymizer p) : INameHider
    {
        public string Hide(string text) => p.Hide(text, []);
        public string Restore(string text) => p.Restore(text, out _);
    }
}
