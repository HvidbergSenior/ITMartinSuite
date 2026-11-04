using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using ITMartinSnak.Application;
using ITMartinSnak.Domain;
using Microsoft.Extensions.Logging;
using WebPush;

namespace ITMartinSnak.Infrastructure;

/// <summary>Everyone with a room open gets each change in that room at once (server-sent events).</summary>
public sealed class SseLiveFeed : ILiveFeed
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<Guid, (string Room, Channel<string> Ch)> _listeners = new();

    public (Guid Id, ChannelReader<string> Reader) Join(string room)
    {
        var ch = Channel.CreateBounded<string>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
        var id = Guid.NewGuid();
        _listeners[id] = (room, ch);
        return (id, ch.Reader);
    }

    public void Leave(Guid id) => _listeners.TryRemove(id, out _);

    public int Count => _listeners.Count;

    public void Send(string room, string kind, object data)
    {
        var line = $"event: {kind}\ndata: {JsonSerializer.Serialize(data, WebJson)}\n\n";
        foreach (var l in _listeners.Values)
            if (l.Room == room) l.Ch.Writer.TryWrite(line);
    }
}

/// <summary>
/// Push to the phones. Keys are made on first start and kept in the data folder, so phones stay subscribed across deploys.
/// A phone belongs to ONE room - or to the host (Room "" = Martin, every room). Phones subscribed before rooms existed
/// (2026-10-09 group) belong to the old group's room only, so they never see a customer's messages. A phone that is gone
/// (410/404) is dropped.
/// </summary>
public sealed class WebPushSender : IPushSender
{
    private readonly string _keysFile, _subsFile;
    private readonly object _gate = new();
    private readonly VapidDetails _vapid;
    private readonly WebPushClient _client = new();
    private readonly ILogger<WebPushSender> _log;
    private List<Subscriber> _subs = [];

    public sealed record Subscriber(string Endpoint, string P256dh, string Auth, string Name, DateTime AddedAt, string? Room = null);
    private sealed record VapidKeys(string PublicKey, string PrivateKey);

    public WebPushSender(string dataDir, ILogger<WebPushSender> log)
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
        _subs = _subs.Select(s => s.Room is null ? s with { Room = RoomRules.LegacyRoomId } : s).ToList();
    }

    public string PublicKey => _vapid.PublicKey;
    public int Count { get { lock (_gate) return _subs.Count; } }

    // One phone can be in several rooms (and be the host's) - one row per phone + room.
    public void Subscribe(string room, string endpoint, string p256dh, string auth, string name)
    {
        lock (_gate)
        {
            _subs.RemoveAll(x => x.Endpoint == endpoint && x.Room == room);
            _subs.Add(new Subscriber(endpoint, p256dh, auth, name, DateTime.UtcNow, room));
            Save();
        }
    }

    public void Unsubscribe(string endpoint)
    {
        lock (_gate) { _subs.RemoveAll(x => x.Endpoint == endpoint); Save(); }
    }

    public void ForgetRoom(string room)
    {
        lock (_gate) { if (_subs.RemoveAll(x => x.Room == room) > 0) Save(); }
    }

    private void Save() => File.WriteAllText(_subsFile, JsonSerializer.Serialize(_subs));

    /// <summary>To the room's phones and the host's phones, except the one that caused it. Runs in the background.</summary>
    public void SendToOthers(string room, string? exceptEndpoint, string title, string body, string tag, string url)
    {
        List<Subscriber> targets;
        lock (_gate)
            targets = _subs.Where(s => (s.Room == room || s.Room == "") && s.Endpoint != exceptEndpoint)
                .GroupBy(s => s.Endpoint).Select(g => g.First()).ToList();
        if (targets.Count == 0) return;
        if (body.Length > 140) body = body[..140] + "…";
        var payload = JsonSerializer.Serialize(new { title, body, url, tag });
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
                    Unsubscribe(s.Endpoint);
                }
                catch (Exception ex)
                {
                    _log.LogWarning("Snak push to {Name} failed: {Error}", s.Name, ex.Message);
                }
            }
        });
    }
}
