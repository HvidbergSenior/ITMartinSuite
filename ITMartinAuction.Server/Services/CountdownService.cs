using ITMartinAuction.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ITMartinAuction.Server.Services;

public sealed class CountdownService(IHubContext<AuctionHub> hub) : IDisposable
{
    private readonly Dictionary<Guid, CancellationTokenSource> _timers = new();
    private readonly object _lock = new();

    public void Start(string code, Guid itemId, int seconds = 30) => Restart(code, itemId, seconds);
    public void Extend(string code, Guid itemId, int seconds = 15) => Restart(code, itemId, seconds);

    public void Cancel(Guid itemId)
    {
        lock (_lock)
        {
            if (!_timers.TryGetValue(itemId, out var cts)) return;
            cts.Cancel();
            cts.Dispose();
            _timers.Remove(itemId);
        }
    }

    private void Restart(string code, Guid itemId, int seconds)
    {
        Cancel(itemId);
        var cts = new CancellationTokenSource();
        lock (_lock) _timers[itemId] = cts;
        _ = RunAsync(code.ToUpper(), itemId, seconds, cts.Token);
    }

    private async Task RunAsync(string code, Guid itemId, int seconds, CancellationToken ct)
    {
        for (var rem = seconds; rem >= 0 && !ct.IsCancellationRequested; rem--)
        {
            await hub.Clients.Group(code).SendAsync("Countdown", itemId, rem, cancellationToken: ct)
                     .ConfigureAwait(false);
            if (rem == 0) break;
            try { await Task.Delay(1000, ct); }
            catch (OperationCanceledException) { return; }
        }
        if (!ct.IsCancellationRequested)
            await hub.Clients.Group(code).SendAsync("ItemTimeout", itemId).ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var cts in _timers.Values) { cts.Cancel(); cts.Dispose(); }
            _timers.Clear();
        }
    }
}
