using System.Text.Json;
using ITMartinElPriser.Core;

namespace ITMartinMitEl.Server.Services;

// One phone that asked for notifications. Anyone with the link can join;
// each device subscribes on its own and picks what it wants to hear about.
public sealed class PushSubscriber
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public bool NotifyCheapest { get; set; } = true;
    public bool NotifyExpensive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSentAt { get; set; }
    public int Failures { get; set; }
}

public sealed class HouseholdData
{
    public List<RunEntry> Runs { get; set; } = [];
    public HouseholdSettings Settings { get; set; } = new();
    public List<Appliance> Appliances { get; set; } = Appliance.Defaults();
    public List<Device> Devices { get; set; } = [];
    public List<PushSubscriber> Subscribers { get; set; } = [];
    // "<subscriberId>|<kind>|<yyyy-MM-dd HH:mm>" of pushes already sent, so a
    // restart never double-sends and a day is only announced once.
    public List<string> SentPushes { get; set; } = [];
}

// Whole state in one JSON file on the data volume, same no-login single-
// household pattern the rest of this app always used. Tiny data, so the
// simplicity beats a database.
public sealed class HouseholdStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private HouseholdData _data;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public HouseholdStore(IConfiguration config)
    {
        var dataDir = config["DataDir"] ?? "/data";
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "household.json");
        _data = Load();
    }

    public HouseholdData Get()
    {
        lock (_lock) return _data;
    }

    public void Update(Action<HouseholdData> change)
    {
        lock (_lock)
        {
            change(_data);
            // Keep the sent-log from growing forever: three days is plenty.
            var cutoff = DkTime.Now.AddDays(-3).ToString("yyyy-MM-dd");
            _data.SentPushes.RemoveAll(s => string.CompareOrdinal(s.Split('|').Last(), cutoff) < 0);
            File.WriteAllText(_path, JsonSerializer.Serialize(_data, JsonOpts));
        }
    }

    private HouseholdData Load()
    {
        if (!File.Exists(_path)) return new HouseholdData();
        try
        {
            var d = JsonSerializer.Deserialize<HouseholdData>(File.ReadAllText(_path)) ?? new HouseholdData();
            if (d.Appliances.Count == 0) d.Appliances = Appliance.Defaults();
            return d;
        }
        catch
        {
            return new HouseholdData();
        }
    }
}
