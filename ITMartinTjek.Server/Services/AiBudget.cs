namespace ITMartinTjek.Server.Services;

/// <summary>Hard cap on Claude calls per UTC day. Anyone on the internet can create
/// a forløb, so the ceiling lives in code, not in a comment.</summary>
public sealed class AiBudget(IConfiguration config, ILogger<AiBudget> logger)
{
    private readonly int _maxPerDay = config.GetValue("Tjek:MaxAiCallsPerDay", 100);
    private readonly object _lock = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private int _used;

    public int Used { get { lock (_lock) { Roll(); return _used; } } }
    public int Max => _maxPerDay;

    /// <summary>Reserve one call. Throws when the day's budget is spent.</summary>
    public void Take(string what)
    {
        lock (_lock)
        {
            Roll();
            if (_used >= _maxPerDay)
            {
                logger.LogWarning("AI budget spent ({Used}/{Max}) - refused {What}", _used, _maxPerDay, what);
                throw new AiBudgetExceededException();
            }
            _used++;
        }
    }

    private void Roll()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today != _day) { _day = today; _used = 0; }
    }
}

public sealed class AiBudgetExceededException()
    : Exception("Dagens AI-kvote er brugt op. Prøv igen i morgen.");
