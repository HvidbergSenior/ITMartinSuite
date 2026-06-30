using System.Net;
using System.Text.Json;
using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace ITMartinClub.Server.Services;

public sealed class ClubPushService
{
    private readonly string _vapidFile;
    private (string Public, string Private)? _keys;

    public ClubPushService()
    {
        var dir = Directory.Exists("/app/data") ? "/app/data" : Path.Combine(Directory.GetCurrentDirectory(), "data");
        Directory.CreateDirectory(dir);
        _vapidFile = Path.Combine(dir, "club-vapid.json");
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

        var gen = VapidHelper.GenerateVapidKeys();
        File.WriteAllText(_vapidFile, JsonSerializer.Serialize(new StoredKeys(gen.PublicKey, gen.PrivateKey)));
        return (_keys = (gen.PublicKey, gen.PrivateKey)).Value;
    }

    public async Task SendToGroupAsync(ClubDbContext db, Guid groupId, string? excludeMember, string title, string body)
    {
        var subs = await db.PushSubscriptions
            .Where(s => s.GroupId == groupId && (excludeMember == null || s.MemberName != excludeMember))
            .ToListAsync();

        if (subs.Count == 0) return;

        var (pub, prv) = Keys();
        var vapid = new VapidDetails("https://lions-club.itmartin.dk", pub, prv);
        var payload = JsonSerializer.Serialize(new { title, body });
        var client = new WebPushClient();

        foreach (var sub in subs)
        {
            try
            {
                await client.SendNotificationAsync(
                    new WebPush.PushSubscription(sub.Endpoint, sub.P256DH, sub.Auth),
                    payload, vapid);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                db.PushSubscriptions.Remove(sub);
                await db.SaveChangesAsync();
            }
            catch { }
        }
    }

    public async Task UpsertSubscriptionAsync(ClubDbContext db, ClubPushSubscription incoming)
    {
        var existing = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.GroupId == incoming.GroupId && s.Endpoint == incoming.Endpoint);
        if (existing is null)
        {
            db.PushSubscriptions.Add(incoming);
        }
        else
        {
            existing.MemberName = incoming.MemberName;
            existing.P256DH = incoming.P256DH;
            existing.Auth = incoming.Auth;
        }
        await db.SaveChangesAsync();
    }

    private sealed record StoredKeys(string Pub, string Prv);
}
