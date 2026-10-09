using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using WebPush;

namespace ITMartinSnak.Server;

public static class Json
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}

/// <summary>Everyone with the page open gets each change at once (server-sent events).</summary>
public sealed class Live
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _listeners = new();

    public (Guid id, ChannelReader<string> reader) Join()
    {
        var ch = Channel.CreateBounded<string>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
        var id = Guid.NewGuid();
        _listeners[id] = ch;
        return (id, ch.Reader);
    }

    public void Leave(Guid id) => _listeners.TryRemove(id, out _);

    public int Count => _listeners.Count;

    public void Send(string kind, object data)
    {
        var line = $"event: {kind}\ndata: {JsonSerializer.Serialize(data, Json.Web)}\n\n";
        foreach (var ch in _listeners.Values) ch.Writer.TryWrite(line);
    }
}

/// <summary>
/// Push to the phones. Keys are made on first start and kept in the data folder, so phones stay
/// subscribed across deploys. A phone that is gone (410/404) is dropped.
/// </summary>
public sealed class PushStore
{
    private readonly string _keysFile, _subsFile;
    private readonly object _gate = new();
    private readonly VapidDetails _vapid;
    private readonly WebPushClient _client = new();
    private readonly ILogger<PushStore> _log;
    private List<Subscriber> _subs = [];

    public sealed record Subscriber(string Endpoint, string P256dh, string Auth, string Name, DateTime AddedAt);
    private sealed record VapidKeys(string PublicKey, string PrivateKey);

    public PushStore(string dataDir, ILogger<PushStore> log)
    {
        _log = log;
        Directory.CreateDirectory(dataDir);
        _keysFile = Path.Combine(dataDir, "vapid.json");
        _subsFile = Path.Combine(dataDir, "push-subscribers.json");
        VapidKeys k;
        if (File.Exists(_keysFile)) k = JsonSerializer.Deserialize<VapidKeys>(File.ReadAllText(_keysFile))!;
        else
        {
            var g = VapidHelper.GenerateVapidKeys();
            k = new VapidKeys(g.PublicKey, g.PrivateKey);
            File.WriteAllText(_keysFile, JsonSerializer.Serialize(k));
        }
        _vapid = new VapidDetails("mailto:ITMartin@Mensa.dk", k.PublicKey, k.PrivateKey);
        if (File.Exists(_subsFile))
            try { _subs = JsonSerializer.Deserialize<List<Subscriber>>(File.ReadAllText(_subsFile)) ?? []; } catch { }
    }

    public string PublicKey => _vapid.PublicKey;
    public int Count { get { lock (_gate) return _subs.Count; } }

    public void Add(Subscriber s)
    {
        lock (_gate) { _subs.RemoveAll(x => x.Endpoint == s.Endpoint); _subs.Add(s); Save(); }
    }

    public void Remove(string endpoint)
    {
        lock (_gate) { _subs.RemoveAll(x => x.Endpoint == endpoint); Save(); }
    }

    private void Save() => File.WriteAllText(_subsFile, JsonSerializer.Serialize(_subs));

    /// <summary>Sends to every phone except the one that caused it. Runs in the background.</summary>
    public void SendToOthers(string? exceptEndpoint, string title, string body, string tag)
    {
        List<Subscriber> targets;
        lock (_gate) targets = _subs.Where(s => s.Endpoint != exceptEndpoint).ToList();
        if (targets.Count == 0) return;
        if (body.Length > 140) body = body[..140] + "…";
        var payload = JsonSerializer.Serialize(new { title, body, url = "/", tag });
        _ = Task.Run(async () =>
        {
            foreach (var s in targets)
            {
                try
                {
                    await _client.SendNotificationAsync(new PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, _vapid);
                }
                catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
                {
                    Remove(s.Endpoint);
                }
                catch (Exception ex)
                {
                    _log.LogWarning("Snak push to {Name} failed: {Error}", s.Name, ex.Message);
                }
            }
        });
    }
}
