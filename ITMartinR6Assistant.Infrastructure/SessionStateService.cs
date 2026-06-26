namespace ITMartinR6Assistant.Infrastructure;

public class SessionStateService
{
    private readonly object _lock = new();

    public string? Map { get; private set; }
    public string? Site { get; private set; }
    public string Side { get; private set; } = "Attack";
    public HashSet<string> Bans { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public int? ActivePlan { get; private set; }

    public event Action? OnStateChanged;

    public void SetMap(string map)
    {
        lock (_lock)
        {
            Map = map;
            Site = null;
            ActivePlan = null;
        }
        NotifyStateChanged();
    }

    public void SetSite(string site)
    {
        lock (_lock)
        {
            Site = site;
            ActivePlan = null;
        }
        NotifyStateChanged();
    }

    public void SetSide(string side)
    {
        lock (_lock) { Side = side; }
        NotifyStateChanged();
    }

    public void ToggleBan(string operatorName)
    {
        lock (_lock)
        {
            if (!Bans.Remove(operatorName))
                Bans.Add(operatorName);
        }
        NotifyStateChanged();
    }

    public void SetActivePlan(int? planNumber)
    {
        lock (_lock) { ActivePlan = planNumber; }
        NotifyStateChanged();
    }

    public void Reset()
    {
        lock (_lock)
        {
            Map = null;
            Site = null;
            Side = "Attack";
            Bans.Clear();
            ActivePlan = null;
        }
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
