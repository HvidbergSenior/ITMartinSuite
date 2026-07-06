using System.Collections.Concurrent;
using WebPush;

namespace ITMartinLive.Server.Services;

public sealed class PushService
{
    private readonly ConcurrentDictionary<string, List<WebPush.PushSubscription>> _subs = new();
    private readonly WebPushClient _client = new();
    private readonly ILogger<PushService> _logger;
    private readonly string _subject;
    private readonly string _vapidPrivate;

    public string VapidPublicKey  { get; }
    public bool   IsConfigured    { get; }

    public PushService(IConfiguration config, ILogger<PushService> logger)
    {
        _logger  = logger;
        _subject = config["Push:Subject"] ?? "mailto:admin@itmartin.dk";
        var pub  = config["Push:VapidPublicKey"];
        var priv = config["Push:VapidPrivateKey"];

        if (string.IsNullOrEmpty(pub) || string.IsNullOrEmpty(priv))
        {
            var keys = VapidHelper.GenerateVapidKeys();
            pub  = keys.PublicKey;
            priv = keys.PrivateKey;
            _logger.LogWarning(
                "No VAPID keys in config — generated new ones (not persistent). " +
                "Set Push__VapidPublicKey={Pub} and Push__VapidPrivateKey in env.", pub);
            IsConfigured = false;
        }
        else
        {
            IsConfigured = true;
        }

        VapidPublicKey = pub;
        _vapidPrivate  = priv;
    }

    public void Subscribe(string slug, string endpoint, string p256dh, string auth)
    {
        var list = _subs.GetOrAdd(slug, _ => []);
        lock (list)
        {
            if (!list.Any(s => s.Endpoint == endpoint))
                list.Add(new WebPush.PushSubscription(endpoint, p256dh, auth));
        }
    }

    public async Task SendAsync(string slug, string title, string body)
    {
        if (!_subs.TryGetValue(slug, out var list)) return;
        var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body });
        var details = new VapidDetails(_subject, VapidPublicKey, _vapidPrivate);
        List<WebPush.PushSubscription> snapshot;
        lock (list) snapshot = [.. list];
        foreach (var sub in snapshot)
        {
            try { await _client.SendNotificationAsync(sub, payload, details); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push failed — removing subscription");
                lock (list) list.Remove(sub);
            }
        }
    }

    public int SubscriberCount(string slug) =>
        _subs.TryGetValue(slug, out var l) ? l.Count : 0;
}
