using System.Text.Json;
using System.Text.Json.Serialization;
using ITMartinHusk.Application;
using ITMartinHusk.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebPush;

namespace ITMartinHusk.Infrastructure;

public sealed record HuskInfraOptions(string DataDir);

// The whole list in one JSON file on the data volume (DataDir/husk.json) - small, and easy to back up.
public sealed class JsonHuskStore : IHuskStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly string _path;
    private readonly object _lock = new();
    private readonly HuskData _data;

    public JsonHuskStore(HuskInfraOptions o)
    {
        Directory.CreateDirectory(o.DataDir);
        _path = Path.Combine(o.DataDir, "husk.json");
        _data = File.Exists(_path) ? JsonSerializer.Deserialize<HuskData>(File.ReadAllText(_path), Json) ?? new HuskData() : new HuskData();
    }

    public HuskData Get()
    {
        lock (_lock) return _data;
    }

    public void Update(Action<HuskData> change)
    {
        lock (_lock)
        {
            change(_data);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_data, Json));
            File.Move(tmp, _path, overwrite: true);
        }
    }
}

// Web Push (VAPID), as in ElPriser. The keys are made once and kept in DataDir/vapid.json - new keys would silently
// orphan every phone that already signed up.
public sealed class WebPushSender : IPushSender
{
    private readonly VapidDetails _vapid;
    private readonly WebPushClient _client = new();
    private readonly IHuskStore _store;
    private readonly ILogger<WebPushSender> _log;

    public string PublicKey => _vapid.PublicKey;

    public WebPushSender(HuskInfraOptions o, IHuskStore store, ILogger<WebPushSender> log)
    {
        _store = store;
        _log = log;
        var path = Path.Combine(o.DataDir, "vapid.json");
        VapidKeys k;
        if (File.Exists(path)) k = JsonSerializer.Deserialize<VapidKeys>(File.ReadAllText(path))!;
        else
        {
            var made = VapidHelper.GenerateVapidKeys();
            k = new VapidKeys(made.PublicKey, made.PrivateKey);
            File.WriteAllText(path, JsonSerializer.Serialize(k));
        }
        _vapid = new VapidDetails("mailto:ITMartin@Mensa.dk", k.PublicKey, k.PrivateKey);
    }

    private sealed record VapidKeys(string PublicKey, string PrivateKey);

    public async Task<bool> SendAsync(HuskSubscriber to, PushMessage msg, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { title = msg.Title, body = msg.Body, url = msg.Url });
        try
        {
            await _client.SendNotificationAsync(new PushSubscription(to.Endpoint, to.P256dh, to.Auth), payload, _vapid, ct);
            return true;
        }
        catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
        {
            _log.LogInformation("A phone is gone ({Status}), removing it", ex.StatusCode);
            _store.Update(d => d.Subscribers.RemoveAll(x => x.Id == to.Id));
            return false;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Push failed");
            _store.Update(d => { if (d.Subscribers.FirstOrDefault(x => x.Id == to.Id) is { } s) s.Failures++; });
            return false;
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddHuskInfrastructure(this IServiceCollection services, HuskInfraOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IHuskStore, JsonHuskStore>();
        services.AddSingleton<IPushSender, WebPushSender>();
        return services;
    }
}
