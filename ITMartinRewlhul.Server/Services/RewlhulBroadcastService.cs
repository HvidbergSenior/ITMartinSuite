namespace ITMartinRewlhul.Server.Services;

// Payload-free pub/sub, same pattern as ITMartinClub.Server's
// ClubBroadcastService - "something changed in this room, go re-query" -
// keyed by room code instead of a group id.
public sealed class RewlhulBroadcastService
{
    private readonly Dictionary<string, List<Action>> _subscribers = new();
    private readonly object _lock = new();

    public void Subscribe(string roomCode, Action callback)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(roomCode, out var list))
                _subscribers[roomCode] = list = [];
            list.Add(callback);
        }
    }

    public void Unsubscribe(string roomCode, Action callback)
    {
        lock (_lock)
        {
            if (_subscribers.TryGetValue(roomCode, out var list))
                list.Remove(callback);
        }
    }

    public void Broadcast(string roomCode)
    {
        List<Action> callbacks;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(roomCode, out var list)) return;
            callbacks = [.. list];
        }
        foreach (var cb in callbacks)
        {
            try { cb(); } catch { }
        }
    }
}
