using System.Text.Json;
using ITMartinHjem.Server.Data;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace ITMartinHjem.Server.Services;

// Web Push to the owner's own devices (same VAPID pattern as MinElpris). Keys are made once
// and kept on the data volume - new keys would orphan every subscribed phone.
public sealed class PushService
{
    private readonly VapidDetails _vapid;
    private readonly WebPushClient _client = new();
    private readonly IDbContextFactory<HjemDb> _db;
    private readonly ILogger<PushService> _log;

    public string PublicKey => _vapid.PublicKey;

    public PushService(IConfiguration cfg, IDbContextFactory<HjemDb> db, ILogger<PushService> log)
    {
        _db = db;
        _log = log;
        var dir = cfg["Hjem:DataDir"] ?? "/app/data";
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "vapid.json");
        if (!File.Exists(path))
        {
            var k = VapidHelper.GenerateVapidKeys();
            File.WriteAllText(path, JsonSerializer.Serialize(new Keys(k.PublicKey, k.PrivateKey)));
        }
        var keys = JsonSerializer.Deserialize<Keys>(File.ReadAllText(path))!;
        _vapid = new VapidDetails("mailto:ITMartin@Mensa.dk", keys.PublicKey, keys.PrivateKey);
    }

    private sealed record Keys(string PublicKey, string PrivateKey);

    // One visitor's browser. Returns false when the subscription is gone (the visitor turned it off).
    public async Task<bool> SendToAsync(string endpoint, string p256dh, string auth, string title, string body, string url)
    {
        try
        {
            await _client.SendNotificationAsync(new PushSubscription(endpoint, p256dh, auth),
                JsonSerializer.Serialize(new { title, body, url }), _vapid);
            _log.LogInformation("Visitor push accepted by {Host}", new Uri(endpoint).Host);
            return true;
        }
        catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound) { return false; }
        catch (Exception ex) { _log.LogWarning(ex, "Visitor push failed"); return true; }
    }

    public async Task<int> SendToOwnerAsync(string title, string body, string url)
    {
        await using var db = await _db.CreateDbContextAsync();
        var payload = JsonSerializer.Serialize(new { title, body, url });
        var sent = 0;
        foreach (var s in await db.PushSubs.ToListAsync())
        {
            try
            {
                await _client.SendNotificationAsync(new PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, _vapid);
                sent++;
                _log.LogInformation("Push accepted by {Host}: {Title}", new Uri(s.Endpoint).Host, title);
            }
            catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
            {
                _log.LogWarning("Push subscription gone ({Status}) - removed; the phone must turn notifications on again", ex.StatusCode);
                db.PushSubs.Remove(s);
            }
            catch (WebPushException ex)
            {
                _log.LogWarning("Push refused by {Host}: {Status} {Body}", new Uri(s.Endpoint).Host, ex.StatusCode,
                    ex.HttpResponseMessage is { } r ? await r.Content.ReadAsStringAsync() : "");
            }
            catch (Exception ex) { _log.LogWarning(ex, "Push failed"); }
        }
        await db.SaveChangesAsync();
        return sent;
    }
}
