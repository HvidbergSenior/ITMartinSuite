using System.Net;
using System.Text.Json;
using ITMartinClub.Application;
using ITMartinClub.Domain;
using ITMartinClub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace ITMartinClub.Infrastructure.External;

// Web push (VAPID keys made once and kept in data/club-vapid.json). Subscriptions that are gone (410/404) are removed.

internal sealed class WebPushNotifier : IPushNotifier
{
    private readonly IDbContextFactory<ClubDb> _dbf;
    private readonly string _vapidFile;
    private (string Public, string Private)? _keys;

    public WebPushNotifier(IDbContextFactory<ClubDb> dbf, string dataDir)
    {
        _dbf = dbf;
        _vapidFile = Path.Combine(dataDir, "club-vapid.json");
    }

    public string PublicKey => Keys().Public;

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

    public async Task ToTeamAsync(Guid groupId, string? exceptMember, string title, string body)
    {
        await using var db = await _dbf.CreateDbContextAsync();
        var subs = await db.PushSubscriptions
            .Where(s => s.GroupId == groupId && (exceptMember == null || s.MemberName != exceptMember))
            .ToListAsync();
        await SendCoreAsync(db, subs, title, body);
    }

    public async Task ToMembersAsync(Guid groupId, IEnumerable<string> memberNames, string title, string body, string? url = null)
    {
        var include = memberNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (include.Count == 0) return;
        await using var db = await _dbf.CreateDbContextAsync();
        var subs = await db.PushSubscriptions
            .Where(s => s.GroupId == groupId)
            .ToListAsync();
        subs = subs.Where(s => include.Contains(s.MemberName)).ToList();
        await SendCoreAsync(db, subs, title, body, url);
    }

    private async Task SendCoreAsync(ClubDb db, List<ClubPushSubscription> subs, string title, string body, string? url = null)
    {
        if (subs.Count == 0) return;

        var (pub, prv) = Keys();
        var vapid = new VapidDetails("https://all-apps.itmartin.dk", pub, prv);
        var payload = JsonSerializer.Serialize(new { title, body, url });   // url: tapping the notification opens it (sw.js)
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

    public async Task SubscribeAsync(ClubPushSubscription incoming, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);
        var existing = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.GroupId == incoming.GroupId && s.Endpoint == incoming.Endpoint, ct);
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
        await db.SaveChangesAsync(ct);
    }

    private sealed record StoredKeys(string Pub, string Prv);
}
