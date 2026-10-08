namespace ITMartinTilbud.Domain;

// One item marked down because its date is near ("madspild"), in one store.
public sealed record Clearance(
    string Store, string Brand, string? Address, string Title, decimal NewPrice, decimal? OriginalPrice,
    decimal? PercentDiscount, decimal? Stock, string? StockUnit, DateTimeOffset? EndTime, string? Image,
    DateTimeOffset? StartTime = null, DateTimeOffset? LastUpdate = null, string? Category = null, string? Ean = null, double? Km = null,
    double? Lat = null, double? Lng = null);   // the store's position - the page measures the distance from the user itself

public static class Clearances
{
    /// <summary>Still for sale: not expired, has a price, not sold out. Biggest discount first.</summary>
    public static List<Clearance> ForSale(IEnumerable<Clearance> all, DateTimeOffset now) =>
        all.Where(c => (c.EndTime is not { } t || t >= now) && c.NewPrice > 0 && c.Stock is not <= 0)
           .OrderByDescending(x => x.PercentDiscount ?? 0).ToList();

    // Salling sends "40+ ØKO.RYGEOST LØGISMOSE" - all capitals is hard to read; first letter big, the rest small.
    public static string Readable(string t) =>
        t.Length > 1 && t.Any(char.IsLetter) && !t.Any(char.IsLower) ? char.ToUpper(t[0]) + t[1..].ToLowerInvariant() : t;

    public static string Brand(string brand) => brand.ToLowerInvariant() switch
    {
        "netto" => "Netto", "foetex" or "føtex" => "føtex", "bilka" => "Bilka", "salling" => "Salling", _ => brand,
    };

    /// <summary>Salling allows only 100 requests a day, so the place is rounded to ~5 km squares and the radius to
    /// 5/10/20 km - nearby visitors share one answer.</summary>
    public static (double Lat, double Lng, int Km) Area(double lat, double lng, int km) =>
        (Math.Round(lat * 20) / 20, Math.Round(lng * 20) / 20, km <= 5 ? 5 : km <= 10 ? 10 : 20);
}

// A deal that comes back every week but is not in any public leaflet - e.g. a weekday deal inside a shop's own app
// (user 2026-10-06: "Milk Tuesday in Lidl ... should be saved in this app"). Martin types them in on /faste.
// Days: 1 = Monday ... 7 = Sunday; empty = every day.
public sealed record FixedDeal(
    string Id, string Chain, string Item, int[] Days, decimal? Price, string? Unit, string? NeedsApp, string? Note, DateTime AddedAt)
{
    public static FixedDeal Create(string chain, string item, IEnumerable<int>? days, decimal? price, string? unit, string? needsApp, string? note, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(chain) || string.IsNullOrWhiteSpace(item)) throw new TilbudException("Skriv både butik og vare.");
        return new FixedDeal(Guid.NewGuid().ToString("N")[..10], chain.Trim(), item.Trim(),
            (days ?? []).Where(x => x is >= 1 and <= 7).Distinct().Order().ToArray(), price is > 0 ? price : null,
            Clean(unit), Clean(needsApp), Clean(note), now);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
