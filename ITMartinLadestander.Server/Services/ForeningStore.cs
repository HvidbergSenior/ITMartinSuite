using System.Text.Json;

namespace ITMartinLadestander.Server.Services;

// Single JSON file, same pattern as ElPriser's HouseholdStore.
public sealed class ForeningStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly ForeningData _data;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public event Action? Changed;

    public ForeningStore(IConfiguration cfg)
    {
        var dir = cfg["DataDir"] ?? "/data";
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "forening.json");
        _data = File.Exists(_path)
            ? JsonSerializer.Deserialize<ForeningData>(File.ReadAllText(_path)) ?? new()
            : new();
        if (SeedChosenSolution(_data) || !File.Exists(_path)) Save();
    }

    // First start after the new solution: 3 stands / 6 outlets (the Looad stand typed in by hand + 2 Zaptec stands)
    // and 30 households with one RFID tag each, which the board renames under Indstillinger.
    private static bool SeedChosenSolution(ForeningData d)
    {
        var changed = false;
        if (d.OutletSetup.Count == 0)
        {
            d.OutletSetup =
            [
                new() { Number = 1, Name = "Looad-stander · udtag 1", Source = "manual", MaxKw = d.OutletKw },
                new() { Number = 2, Name = "Looad-stander · udtag 2", Source = "manual", MaxKw = d.OutletKw },
                new() { Number = 3, Name = "Stander B · udtag 1", Source = "zaptec" },
                new() { Number = 4, Name = "Stander B · udtag 2", Source = "zaptec" },
                new() { Number = 5, Name = "Stander C · udtag 1", Source = "zaptec" },
                new() { Number = 6, Name = "Stander C · udtag 2", Source = "zaptec" },
            ];
            d.Outlets = d.OutletSetup.Count;
            changed = true;
        }
        if (d.HouseholdList.Count == 0)
        {
            d.HouseholdList = Enumerable.Range(1, d.Households).Select(i => new Household { Name = $"Hus {i:00}", Tokens = [$"Brik {i:00}"] }).ToList();
            changed = true;
        }
        return changed;
    }

    public ForeningData Get() { lock (_gate) return _data; }

    public void Update(Action<ForeningData> mutate)
    {
        lock (_gate) { mutate(_data); Save(); }
        Changed?.Invoke();
    }

    private void Save()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_data, Json));
        File.Move(tmp, _path, overwrite: true);
    }
}
