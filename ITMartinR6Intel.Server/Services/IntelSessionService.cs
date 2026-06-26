using ITMartinR6Intel.Server.Models;

namespace ITMartinR6Intel.Server.Services;

public class IntelSessionService
{
    private readonly object _lock = new();

    public string? SelectedMap { get; private set; }
    public string ActiveFloor { get; private set; } = "1F";
    public List<IntelMarker> Markers { get; private set; } = [];
    public string LiveNote { get; private set; } = "";

    public event Action? OnStateChanged;

    public void SetMap(string? map)
    {
        lock (_lock) { SelectedMap = map; Markers.Clear(); }
        Notify();
    }

    public void SetFloor(string floor)
    {
        lock (_lock) { ActiveFloor = floor; }
        Notify();
    }

    public void AddMarker(IntelMarker marker)
    {
        lock (_lock) { Markers.Add(marker); }
        Notify();
    }

    public void RemoveMarker(Guid id)
    {
        lock (_lock) { Markers.RemoveAll(m => m.Id == id); }
        Notify();
    }

    public void ClearMarkers()
    {
        lock (_lock) { Markers.Clear(); }
        Notify();
    }

    public void ClearFloorMarkers()
    {
        lock (_lock) { Markers.RemoveAll(m => m.Floor == ActiveFloor); }
        Notify();
    }

    public void SetLiveNote(string note)
    {
        lock (_lock) { LiveNote = note; }
        Notify();
    }

    private void Notify() => OnStateChanged?.Invoke();
}
