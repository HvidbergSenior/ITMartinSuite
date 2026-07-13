using System.Text.Json;

namespace ITMartinElPriser.Server.Services;

public sealed class PriceSettings
{
    public string GridCompanyId { get; set; } = "radius";
    public double CustomNettarifOre { get; set; } = 25;
    public string SupplierId { get; set; } = "norlys-flexel";
    public double CustomTillaegOre { get; set; }
    public double AnnualUsageKwh { get; set; } = 4000;
}

// Single-household settings, no login - same pattern as PriceHistoryStore/RunLogStore.
public sealed class PriceSettingsStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private PriceSettings _settings;

    public PriceSettingsStore(IConfiguration config)
    {
        var dataDir = config["DataDir"] ?? "/data";
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "price-settings.json");
        _settings = Load();
    }

    public PriceSettings Get()
    {
        lock (_lock) return _settings;
    }

    public void Save(PriceSettings settings)
    {
        lock (_lock)
        {
            _settings = settings;
            File.WriteAllText(_path, JsonSerializer.Serialize(settings));
        }
    }

    private PriceSettings Load()
    {
        if (!File.Exists(_path)) return new PriceSettings();

        try
        {
            return JsonSerializer.Deserialize<PriceSettings>(File.ReadAllText(_path)) ?? new PriceSettings();
        }
        catch
        {
            return new PriceSettings();
        }
    }
}
