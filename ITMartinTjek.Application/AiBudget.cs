using Microsoft.Extensions.Logging;

namespace ITMartinTjek.Application;

/// <summary>Hard cap on Claude calls per UTC day. Anyone on the internet can ask,
/// so the ceiling lives in code, not in a comment.</summary>
public sealed class AiBudget(int maxPerDay, TimeProvider clock, ILogger<AiBudget> logger)
{
    private readonly object _lock = new();
    private DateOnly _day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private int _used;

    public int Used { get { lock (_lock) { Roll(); return _used; } } }
    public int Max => maxPerDay;

    /// <summary>Reserve one call. Throws when the day's budget is spent.</summary>
    public void Take(string what)
    {
        lock (_lock)
        {
            Roll();
            if (_used >= maxPerDay)
            {
                logger.LogWarning("AI budget spent ({Used}/{Max}) - refused {What}", _used, maxPerDay, what);
                throw new AiBudgetExceededException();
            }
            _used++;
        }
    }

    private void Roll()
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (today != _day) { _day = today; _used = 0; }
    }
}

public sealed class AiBudgetExceededException()
    : Exception("Dagens AI-kvote er brugt op. Prøv igen i morgen.");
