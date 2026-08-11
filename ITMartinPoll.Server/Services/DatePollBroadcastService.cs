namespace ITMartinPoll.Server.Services;

public sealed class DatePollBroadcastService
{
    private readonly Dictionary<int, List<Action>> _subscribers = new();
    private readonly object _lock = new();

    public void Subscribe(int datePollId, Action callback)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(datePollId, out var list))
                _subscribers[datePollId] = list = [];
            list.Add(callback);
        }
    }

    public void Unsubscribe(int datePollId, Action callback)
    {
        lock (_lock)
        {
            if (_subscribers.TryGetValue(datePollId, out var list))
                list.Remove(callback);
        }
    }

    public void Broadcast(int datePollId)
    {
        List<Action> callbacks;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(datePollId, out var list)) return;
            callbacks = [.. list];
        }
        foreach (var cb in callbacks)
        {
            try { cb(); } catch { }
        }
    }
}
