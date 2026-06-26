using ITMartinR6Intel.Server.Models;

namespace ITMartinR6Intel.Server.Services;

public class IntelSessionService
{
    private readonly object _lock = new();

    public string? SelectedMap { get; private set; }
    public string ActiveFloor { get; private set; } = "1F";
    public List<IntelMarker> Markers { get; private set; } = [];
    public string LiveNote { get; private set; } = "";

    public string?[] PlayerNames { get; private set; } = new string?[5]; // P1-P5 operator names
    public string?[] PlayerRoles { get; private set; } = new string?[5]; // P1-P5 role text
    public string? BombCarrier { get; private set; }                      // "P1", "P2", etc.
    public string ActiveMarkerMode { get; private set; } = "Enemy";      // Enemy, Gadget, Caution, Rotate, Player1..Player5, Bomb

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

    public void ClearIntelMarkers()
    {
        lock (_lock) { Markers.RemoveAll(m => m.Type is "Enemy" or "Gadget" or "Caution" or "Rotate"); }
        Notify();
    }

    public void ClearPositionMarkers()
    {
        lock (_lock) { Markers.RemoveAll(m => m.Type is "Player1" or "Player2" or "Player3" or "Player4" or "Player5" or "Bomb"); }
        Notify();
    }

    public void SetLiveNote(string note)
    {
        lock (_lock) { LiveNote = note; }
        Notify();
    }

    public void SetPlayerName(int index, string? name)
    {
        lock (_lock) { PlayerNames[index] = name; }
        Notify();
    }

    public void SetPlayerRole(int index, string? role)
    {
        lock (_lock) { PlayerRoles[index] = role; }
        Notify();
    }

    public void SetBombCarrier(string? player)
    {
        lock (_lock) { BombCarrier = player; }
        Notify();
    }

    public void SetActiveMarkerMode(string mode)
    {
        lock (_lock) { ActiveMarkerMode = mode; }
        Notify();
    }

    private void Notify() => OnStateChanged?.Invoke();
}
