using System.Text.Json;

namespace ITMartinTilbud.Server.Services;

// A deal that comes back every week but is not in any public leaflet - e.g. a weekday deal inside a shop's own app
// (user 2026-10-06: "Milk Tuesday in Lidl ... should be saved in this app"). Martin types them in on /faste.
// Days: 1 = Monday ... 7 = Sunday; empty = every day.
public sealed record FixedDeal(
    string Id, string Chain, string Item, int[] Days, decimal? Price, string? Unit, string? NeedsApp, string? Note, DateTime AddedAt);

public sealed class FixedDeals
{
    private readonly string _file;
    private readonly object _gate = new();
    private List<FixedDeal> _deals;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public FixedDeals(IConfiguration cfg)
    {
        var dir = cfg["Tilbud:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dir);
        _file = Path.Combine(dir, "faste-tilbud.json");
        _deals = File.Exists(_file) ? JsonSerializer.Deserialize<List<FixedDeal>>(File.ReadAllText(_file), Json) ?? [] : [];
    }

    public List<FixedDeal> All() { lock (_gate) return _deals.OrderBy(d => d.Item).ThenBy(d => d.Chain).ToList(); }

    public FixedDeal Add(string chain, string item, int[] days, decimal? price, string? unit, string? needsApp, string? note)
    {
        var d = new FixedDeal(Guid.NewGuid().ToString("N")[..10], chain.Trim(), item.Trim(),
            days.Where(x => x is >= 1 and <= 7).Distinct().Order().ToArray(), price is > 0 ? price : null,
            Clean(unit), Clean(needsApp), Clean(note), DateTime.UtcNow);
        lock (_gate) { _deals.Add(d); Save(); }
        return d;
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            var n = _deals.RemoveAll(d => d.Id == id);
            if (n > 0) Save();
            return n > 0;
        }
    }

    // Write to a temp file first, so a crash mid-write never leaves half a file.
    private void Save()
    {
        var tmp = _file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_deals, Json));
        File.Move(tmp, _file, overwrite: true);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
