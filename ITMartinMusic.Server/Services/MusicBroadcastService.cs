namespace ITMartinMusic.Server.Services;

public sealed class MusicBroadcastService
{
    private readonly List<Func<Task>> _handlers = new();

    public void Subscribe(Func<Task> handler)
    {
        lock (_handlers) _handlers.Add(handler);
    }

    public void Unsubscribe(Func<Task> handler)
    {
        lock (_handlers) _handlers.Remove(handler);
    }

    public async Task BroadcastAsync()
    {
        Func<Task>[] snapshot;
        lock (_handlers) snapshot = [.. _handlers];
        foreach (var h in snapshot)
            try { await h(); } catch { /* disconnected circuit */ }
    }
}
