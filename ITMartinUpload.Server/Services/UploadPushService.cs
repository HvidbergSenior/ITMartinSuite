using System.Net;
using System.Text.Json;
using WebPush;

namespace ITMartinUpload.Server.Services;

public sealed record PushRegistration(string Endpoint, string P256DH, string Auth, string Who);

/// <summary>
/// Web push for the two people in a conversation: Martin gets a ping when a customer
/// writes or uploads, the customer gets one when Martin answers. Subscriptions are
/// files on the data volume - no database for what is at most a handful of devices.
/// </summary>
public sealed class UploadPushService
{
    public const string Martin = "martin";

    private readonly string _dataDir;
    private readonly string _vapidFile;
    private readonly object _lock = new();
    private (string Public, string Private)? _keys;

    public UploadPushService(Microsoft.Extensions.Options.IOptions<UploadOptions> options)
    {
        _dataDir = options.Value.Root;
        Directory.CreateDirectory(_dataDir);
        _vapidFile = Path.Combine(_dataDir, "upload-vapid.json");
    }

    public string GetPublicKey() => Keys().Public;

    private (string Public, string Private) Keys()
    {
        lock (_lock)
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
    }

    /// <summary>"martin" keeps one file of his own; a customer's file lives with their uploads.</summary>
    private string FileFor(string who) =>
        who == Martin
            ? Path.Combine(_dataDir, "martin-push.json")
            : Path.Combine(_dataDir, who.ToLowerInvariant(), "_push.json");

    public IReadOnlyList<PushRegistration> Read(string who)
    {
        if (who != Martin && !UploadStore.IsValidSlug(who)) return [];
        lock (_lock)
        {
            var path = FileFor(who);
            if (!File.Exists(path)) return [];
            try { return JsonSerializer.Deserialize<List<PushRegistration>>(File.ReadAllText(path)) ?? []; }
            catch (JsonException) { return []; }
            catch (IOException) { return []; }
        }
    }

    public void Subscribe(string who, PushRegistration registration)
    {
        if (who != Martin && !UploadStore.IsValidSlug(who)) return;
        if (string.IsNullOrWhiteSpace(registration.Endpoint)) return;

        lock (_lock)
        {
            var all = Read(who)
                .Where(r => r.Endpoint != registration.Endpoint)
                .Append(registration)
                .ToList();
            Save(who, all);
        }
    }

    private void Save(string who, List<PushRegistration> all)
    {
        var path = FileFor(who);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task SendAsync(string who, string title, string body, string? url = null)
    {
        var subs = Read(who);
        if (subs.Count == 0) return;

        var (pub, prv) = Keys();
        var vapid = new VapidDetails("https://upload.itmartin.dk", pub, prv);
        var payload = JsonSerializer.Serialize(new { title, body, url });
        var client = new WebPushClient();
        var dead = new List<string>();

        foreach (var sub in subs)
        {
            try
            {
                await client.SendNotificationAsync(
                    new WebPush.PushSubscription(sub.Endpoint, sub.P256DH, sub.Auth), payload, vapid);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                // The browser threw the subscription away; stop trying to reach it.
                dead.Add(sub.Endpoint);
            }
            catch (Exception)
            {
                // A push that does not arrive must never take an upload down with it.
            }
        }

        if (dead.Count > 0)
            lock (_lock) Save(who, Read(who).Where(r => !dead.Contains(r.Endpoint)).ToList());
    }

    private sealed record StoredKeys(string Pub, string Prv);
}
