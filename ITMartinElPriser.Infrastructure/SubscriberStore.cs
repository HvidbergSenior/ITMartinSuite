using System.Text.Json;
using ITMartinElPriser.Application;
using ITMartinElPriser.Domain;
using Microsoft.Extensions.Configuration;

namespace ITMartinElPriser.Infrastructure;

// The only thing the free app keeps: who wants a message. One JSON file on the data volume (DataDir). On first start it
// adopts the subscribers from the old single-household household.json, so nobody has to sign up again.
public sealed class SubscriberStore : ISubscriberStore
{
    private readonly string _path;
    private readonly string _legacyPath;
    private readonly object _lock = new();
    private readonly SubscriberData _data;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public SubscriberStore(IConfiguration config)
    {
        var dataDir = config["DataDir"] ?? "/data";
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "subscribers.json");
        _legacyPath = Path.Combine(dataDir, "household.json");
        _data = Load();
    }

    public SubscriberData Get()
    {
        lock (_lock) return _data;
    }

    public void Update(Action<SubscriberData> change)
    {
        lock (_lock)
        {
            change(_data);
            _data.Prune(DkTime.Now);
            File.WriteAllText(_path, JsonSerializer.Serialize(_data, JsonOpts));
        }
    }

    private SubscriberData Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<SubscriberData>(File.ReadAllText(_path)) ?? new SubscriberData();
            if (File.Exists(_legacyPath) && JsonSerializer.Deserialize<LegacyHousehold>(File.ReadAllText(_legacyPath)) is { } legacy)
            {
                var settings = legacy.Settings ?? new HouseholdSettings();
                settings.EloverblikToken = ""; settings.MeteringPointId = ""; settings.MeteringPointAddress = "";
                foreach (var s in legacy.Subscribers) s.Settings = settings;
                var d = new SubscriberData { Subscribers = legacy.Subscribers };
                File.WriteAllText(_path, JsonSerializer.Serialize(d, JsonOpts));
                return d;
            }
        }
        catch { }
        return new SubscriberData();
    }

    private sealed class LegacyHousehold
    {
        public HouseholdSettings? Settings { get; set; }
        public List<PushSubscriber> Subscribers { get; set; } = [];
    }
}
