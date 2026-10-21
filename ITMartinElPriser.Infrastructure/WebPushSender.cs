using System.Text.Json;
using ITMartinElPriser.Application;
using ITMartinElPriser.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;

namespace ITMartinElPriser.Infrastructure;

// Web Push (VAPID). The keys are made once and kept in DataDir/vapid.json - new keys would silently orphan every phone
// that already signed up.
public sealed class WebPushSender : IPushSender
{
    private readonly VapidDetails _vapid;
    private readonly WebPushClient _client = new();
    private readonly ISubscriberStore _store;
    private readonly ILogger<WebPushSender> _logger;

    public string PublicKey => _vapid.PublicKey;

    public WebPushSender(IConfiguration config, ISubscriberStore store, ILogger<WebPushSender> logger)
    {
        _store = store;
        _logger = logger;
        var path = Path.Combine(config["DataDir"] ?? "/data", "vapid.json");
        VapidKeys k;
        if (File.Exists(path)) k = JsonSerializer.Deserialize<VapidKeys>(File.ReadAllText(path))!;
        else
        {
            var made = VapidHelper.GenerateVapidKeys();
            k = new VapidKeys(made.PublicKey, made.PrivateKey);
            File.WriteAllText(path, JsonSerializer.Serialize(k));
        }
        _vapid = new VapidDetails("mailto:hvidbergsenior@gmail.com", k.PublicKey, k.PrivateKey);
    }

    private sealed record VapidKeys(string PublicKey, string PrivateKey);

    public async Task<bool> SendAsync(PushSubscriber to, PushMessage msg, CancellationToken ct = default)
    {
        var sub = new PushSubscription(to.Endpoint, to.P256dh, to.Auth);
        var payload = JsonSerializer.Serialize(new { title = msg.Title, body = msg.Body, url = msg.Url });
        try
        {
            await _client.SendNotificationAsync(sub, payload, _vapid, ct);
            _store.Update(d =>
            {
                if (d.Subscribers.FirstOrDefault(x => x.Id == to.Id) is not { } s) return;
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
                if (d.Subscribers.FirstOrDefault(x => x.Id == to.Id) is { } s) s.Failures++;
            });
            return true;
        }
    }
}
