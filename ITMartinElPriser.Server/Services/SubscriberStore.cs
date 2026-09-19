using System.Text.Json;
using ITMartinElPriser.Core;

namespace ITMartinElPriser.Server.Services;

// One phone that asked for notifications. It carries its own pricing
// settings (copied from the cookie when it subscribed) because the server
// has no other memory of who this phone is.
public sealed class PushSubscriber
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public bool NotifyCheapest { get; set; } = true;
    public bool NotifyExpensive { get; set; }
    public HouseholdSettings Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSentAt { get; set; }
    public int Failures { get; set; }
}

public sealed class SubscriberData
{
    public List<PushSubscriber> Subscribers { get; set; } = [];
    // "<subscriberId>|<kind>|<yyyy-MM-dd HH:mm>" of pushes already sent, so a
    // restart never double-sends and a day is only announced once.
    public List<string> SentPushes { get; set; } = [];
}

// The only thing the free app persists: who wants a push. One JSON file on
// the data volume. On first start it adopts the subscribers from the old
// single-household household.json so nobody has to re-subscribe.
public sealed class SubscriberStore
{
    private readonly string _path;
    private readonly string _legacyPath;
    private readonly object _lock = new();
    private SubscriberData _data;

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
            var cutoff = DkTime.Now.AddDays(-3).ToString("yyyy-MM-dd");
            _data.SentPushes.RemoveAll(s => string.CompareOrdinal(s.Split('|').Last(), cutoff) < 0);
            File.WriteAllText(_path, JsonSerializer.Serialize(_data, JsonOpts));
        }
    }

    private SubscriberData Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<SubscriberData>(File.ReadAllText(_path)) ?? new SubscriberData();
            if (File.Exists(_legacyPath))
            {
                var legacy = JsonSerializer.Deserialize<LegacyHousehold>(File.ReadAllText(_legacyPath));
                if (legacy is not null)
                {
                    var settings = legacy.Settings ?? new HouseholdSettings();
                    settings.EloverblikToken = ""; settings.MeteringPointId = ""; settings.MeteringPointAddress = "";
                    foreach (var s in legacy.Subscribers) s.Settings = settings;
                    var d = new SubscriberData { Subscribers = legacy.Subscribers };
                    File.WriteAllText(_path, JsonSerializer.Serialize(d, JsonOpts));
                    return d;
                }
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
