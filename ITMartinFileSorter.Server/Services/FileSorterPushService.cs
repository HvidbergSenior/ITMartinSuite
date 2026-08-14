using System.Net;
using System.Text.Json;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace ITMartinFileSorter.Server.Services;

/// <summary>
/// Web Push for FileSorter - single local install, no per-tenant/family
/// scoping needed (unlike ITMartinFamily/ITMartinClub's push services this
/// is adapted from), so subscriptions and sends are global to this app.
/// VAPID keys are generated once and cached to a local file, same approach
/// as ITMartinFamily.Infrastructure.Services.VapidKeyProvider and
/// ITMartinClub.Server.Services.ClubPushService - no magic.env entry needed.
/// </summary>
public sealed class FileSorterPushService(
    IDbContextFactory<MediaDbContext> dbFactory,
    ILogger<FileSorterPushService> logger)
{
    private readonly string _vapidFile = ResolveVapidFilePath();
    private (string Public, string Private)? _keys;

    private static string ResolveVapidFilePath()
    {
        var dir = Directory.Exists("/app/data") ? "/app/data" : Path.Combine(Directory.GetCurrentDirectory(), "data");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "filesorter-vapid.json");
    }

    public string GetPublicKey() => Keys().Public;

    private (string Public, string Private) Keys()
    {
        if (_keys.HasValue) return _keys.Value;

        if (File.Exists(_vapidFile))
        {
            var stored = JsonSerializer.Deserialize<StoredKeys>(File.ReadAllText(_vapidFile));
            if (stored?.Pub is { Length: > 0 } && stored.Prv is { Length: > 0 })
                return (_keys = (stored.Pub, stored.Prv)).Value;
        }

        var generated = VapidHelper.GenerateVapidKeys();
        File.WriteAllText(_vapidFile, JsonSerializer.Serialize(new StoredKeys(generated.PublicKey, generated.PrivateKey)));
        return (_keys = (generated.PublicKey, generated.PrivateKey)).Value;
    }

    public async Task SubscribeAsync(string endpoint, string p256dh, string auth)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (existing is not null)
        {
            existing.P256DH = p256dh;
            existing.Auth = auth;
        }
        else
        {
            db.PushSubscriptions.Add(new PushSubscriptionEntity
            {
                Endpoint = endpoint,
                P256DH = p256dh,
                Auth = auth
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task UnsubscribeAsync(string endpoint)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.PushSubscriptions.Where(s => s.Endpoint == endpoint).ExecuteDeleteAsync();
    }

    public async Task<bool> IsSubscribedAsync(string endpoint)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.PushSubscriptions.AnyAsync(s => s.Endpoint == endpoint);
    }

    public async Task SendToAllAsync(string title, string body)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var subs = await db.PushSubscriptions.ToListAsync();
        if (subs.Count == 0) return;

        var (pub, prv) = Keys();
        var vapidDetails = new VapidDetails("https://itmartin.dk", pub, prv);
        var payload = JsonSerializer.Serialize(new { title, body });
        var client = new WebPushClient();

        foreach (var sub in subs)
        {
            try
            {
                await client.SendNotificationAsync(
                    new WebPush.PushSubscription(sub.Endpoint, sub.P256DH, sub.Auth),
                    payload,
                    vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                db.PushSubscriptions.Remove(sub);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Push failed for endpoint {Endpoint}", sub.Endpoint[..Math.Min(40, sub.Endpoint.Length)]);
            }
        }
    }

    private sealed record StoredKeys(string Pub, string Prv);
}
