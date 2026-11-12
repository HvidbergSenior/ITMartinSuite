using System.Text.Json;
using System.Text.Json.Serialization;
using ITMartinHusk.Application;
using ITMartinHusk.Domain;
using ITMartinKunder.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebPush;

namespace ITMartinHusk.Infrastructure;

public sealed record HuskInfraOptions(string DataDir);

// One person's whole list in one JSON file - small, and easy to back up. Martin's stays where it always was
// (DataDir/husk.json); a customer's is DataDir/personer/<kundenummer>/husk.json.
public sealed class JsonHuskStore : IHuskStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly string _path;
    private readonly object _lock = new();
    private readonly HuskData _data;

    public JsonHuskStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _path = path;
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

/// <summary>"Se et eksempel": never written to disk, gone when the example is forgotten.</summary>
public sealed class MemoryHuskStore : IHuskStore
{
    private readonly object _lock = new();
    private readonly HuskData _data = new();

    public HuskData Get()
    {
        lock (_lock) return _data;
    }

    public void Update(Action<HuskData> change)
    {
        lock (_lock) change(_data);
    }
}

public sealed class HuskStoreFactory(HuskInfraOptions o) : IHuskStoreFactory
{
    private string People => Path.Combine(o.DataDir, "personer");

    public IHuskStore Open(string person)
    {
        if (HuskPeople.IsDemo(person)) return new MemoryHuskStore();
        if (person == HuskPeople.Martin) return new JsonHuskStore(Path.Combine(o.DataDir, "husk.json"));
        var nr = HuskPeople.Kundenummer(person) ?? throw new ArgumentException("Ukendt person.");   // 8 digits only: it names a folder
        return new JsonHuskStore(Path.Combine(People, nr, "husk.json"));
    }

    public IEnumerable<string> Saved()
    {
        yield return HuskPeople.Martin;
        if (!Directory.Exists(People)) yield break;
        foreach (var dir in Directory.EnumerateDirectories(People).OrderBy(d => d, StringComparer.Ordinal))
        {
            var nr = Path.GetFileName(dir);
            if (HuskPeople.IsKundenummer(nr) && File.Exists(Path.Combine(dir, "husk.json"))) yield return HuskPeople.Kunde(nr);
        }
    }
}

// Kunder on the photoserver (Kunder__Url / Kunder__ApiKey, as ElPriser) answers whether a kundenummer opens Husk.
// Husk must be in Kunder's free apps and switched on.
// The client remembers a yes for 10 minutes, so a new "Husk: betalt" tick in Kunder shows within 10 minutes.
internal sealed class KunderCodes(KunderClient kunder) : ICustomerCodes
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> LastPaid = new();

    public async Task<(bool Ok, string Error, bool Paid)> CheckAsync(string code, CancellationToken ct = default)
    {
        var r = await kunder.CheckCodeAsync(code, "Husk", ct);
        if (r.Ok) LastPaid[code] = r.Paid;
        return (r.Ok, r.Error ?? "", r.Ok && r.Paid);
    }

    public async Task<bool> PaidAsync(string code, CancellationToken ct = default)
    {
        var r = await kunder.CheckCodeAsync(code, "Husk", ct);
        if (r.Ok) return LastPaid[code] = r.Paid;
        return LastPaid.TryGetValue(code, out var last) && last;   // Kunder slow or down: the last answer stands
    }
}

// Web Push (VAPID), as in ElPriser. The keys are made once and kept in DataDir/vapid.json - new keys would silently
// orphan every phone that already signed up.
public sealed class WebPushSender : IPushSender
{
    private readonly VapidDetails _vapid;
    private readonly WebPushClient _client = new();
    private readonly ILogger<WebPushSender> _log;

    public string PublicKey => _vapid.PublicKey;

    public WebPushSender(HuskInfraOptions o, ILogger<WebPushSender> log)
    {
        _log = log;
        Directory.CreateDirectory(o.DataDir);
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

    public async Task<PushResult> SendAsync(HuskSubscriber to, PushMessage msg, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { title = msg.Title, body = msg.Body, url = msg.Url });
        try
        {
            await _client.SendNotificationAsync(new PushSubscription(to.Endpoint, to.P256dh, to.Auth), payload, _vapid, ct);
            return PushResult.Sent;
        }
        catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
        {
            _log.LogInformation("A phone is gone ({Status}), removing it", ex.StatusCode);
            return PushResult.Gone;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Push failed");
            return PushResult.Failed;
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddHuskInfrastructure(this IServiceCollection services, HuskInfraOptions options, IConfiguration config)
    {
        services.AddSingleton(options);
        services.AddKunderClient(config, options.DataDir);
        services.AddSingleton<ICustomerCodes, KunderCodes>();
        services.AddSingleton<IHuskStoreFactory, HuskStoreFactory>();
        services.AddSingleton<IPushSender, WebPushSender>();
        return services;
    }
}
