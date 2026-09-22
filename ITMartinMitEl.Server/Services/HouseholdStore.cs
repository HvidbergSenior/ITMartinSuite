using System.Text.Json;
using ITMartinElPriser.Core;

namespace ITMartinMitEl.Server.Services;

// One phone that asked for notifications. Each device subscribes on its own
// and picks what it wants to hear about.
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

/// <summary>
/// One home's state. Up to phase 1 this was the app's single json file; since accounts
/// arrived there is one of these per household, handed out by <see cref="HouseholdRegistry"/>
/// and written back to the database. The in-memory shape is unchanged, so the pages and
/// the notification jobs did not have to learn anything new.
/// </summary>
public sealed class HouseholdStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly object _lock = new();
    private readonly Action<Guid, string> _persist;
    private HouseholdData _data;

    public Guid HouseholdId { get; }

    public HouseholdStore(Guid householdId, string? json, Action<Guid, string> persist)
    {
        HouseholdId = householdId;
        _persist = persist;
        _data = Parse(json);
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
            _persist(HouseholdId, JsonSerializer.Serialize(_data, JsonOpts));
        }
    }

    public string Serialize()
    {
        lock (_lock) return JsonSerializer.Serialize(_data, JsonOpts);
    }

    public static HouseholdData Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new HouseholdData();
        try
        {
            var d = JsonSerializer.Deserialize<HouseholdData>(json) ?? new HouseholdData();
            if (d.Appliances.Count == 0) d.Appliances = Appliance.Defaults();
            return d;
        }
        catch (JsonException)
        {
            return new HouseholdData();
        }
    }
}
