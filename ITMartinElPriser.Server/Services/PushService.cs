using System.Text.Json;
using WebPush;

namespace ITMartinElPriser.Server.Services;

// Web Push (VAPID). Keys are generated once and kept on the data volume -
// regenerating them would silently orphan every phone that already
// subscribed.
public sealed class PushService
{
    private readonly VapidDetails _vapid;
    private readonly WebPushClient _client = new();
    private readonly HouseholdStore _store;
    private readonly ILogger<PushService> _logger;

    public string PublicKey => _vapid.PublicKey;

    public PushService(IConfiguration config, HouseholdStore store, ILogger<PushService> logger)
    {
        _store = store;
        _logger = logger;
        var dataDir = config["DataDir"] ?? "/data";
        var path = Path.Combine(dataDir, "vapid.json");
        if (File.Exists(path))
        {
            var k = JsonSerializer.Deserialize<VapidKeys>(File.ReadAllText(path))!;
            _vapid = new VapidDetails("mailto:hvidbergsenior@gmail.com", k.PublicKey, k.PrivateKey);
        }
        else
        {
            var k = VapidHelper.GenerateVapidKeys();
            File.WriteAllText(path, JsonSerializer.Serialize(new VapidKeys(k.PublicKey, k.PrivateKey)));
            _vapid = new VapidDetails("mailto:hvidbergsenior@gmail.com", k.PublicKey, k.PrivateKey);
        }
    }

    public sealed record VapidKeys(string PublicKey, string PrivateKey);

    public sealed record Message(string Title, string Body, string Url = "/");

    // Returns false when the endpoint is gone (410/404) - the caller drops it.
    public async Task<bool> SendAsync(PushSubscriber to, Message msg, CancellationToken ct = default)
    {
        var sub = new PushSubscription(to.Endpoint, to.P256dh, to.Auth);
        var payload = JsonSerializer.Serialize(new { title = msg.Title, body = msg.Body, url = msg.Url });
        try
        {
            await _client.SendNotificationAsync(sub, payload, _vapid, ct);
            _store.Update(d =>
            {
                var s = d.Subscribers.FirstOrDefault(x => x.Id == to.Id);
                if (s is null) return;
                s.LastSentAt = DateTime.UtcNow;
                s.Failures = 0;
            });
            return true;
        }
        catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Subscriber {Name} is gone ({Status}), removing", to.Name, ex.StatusCode);
            _store.Update(d => d.Subscribers.RemoveAll(x => x.Id == to.Id));
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push to {Name} failed", to.Name);
            _store.Update(d =>
            {
                var s = d.Subscribers.FirstOrDefault(x => x.Id == to.Id);
                if (s is not null) s.Failures++;
            });
            return true;
        }
    }
}
