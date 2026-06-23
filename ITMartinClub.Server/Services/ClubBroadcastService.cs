namespace ITMartinClub.Server.Services;

public sealed class ClubBroadcastService
{
    private readonly Dictionary<Guid, List<Action>> _subscribers = new();
    private readonly object _lock = new();

    public void Subscribe(Guid groupId, Action callback)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(groupId, out var list))
                _subscribers[groupId] = list = [];
            list.Add(callback);
        }
    }

    public void Unsubscribe(Guid groupId, Action callback)
    {
        lock (_lock)
        {
            if (_subscribers.TryGetValue(groupId, out var list))
                list.Remove(callback);
        }
    }

    public void Broadcast(Guid groupId)
    {
        List<Action> callbacks;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(groupId, out var list)) return;
            callbacks = [.. list];
        }
        foreach (var cb in callbacks)
        {
            try { cb(); } catch { }
        }
    }
}
