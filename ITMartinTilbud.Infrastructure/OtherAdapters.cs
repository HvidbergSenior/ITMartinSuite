using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using ITMartinTilbud.Application;
using ITMartinTilbud.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ITMartinTilbud.Infrastructure;

/// <summary>Salling Group's OFFICIAL Anti Food Waste API (free key from developer.sallinggroup.dev): Netto, føtex and Bilka.
/// Salling allows only 100 requests a day: the area is rounded (Domain), an answer is reused for an hour, and after
/// <see cref="MaxPerDay"/> calls the last answer for that area is shown.</summary>
public sealed class SallingFoodWaste(HttpClient http, IMemoryCache cache, TimeProvider clock, string key, ILogger<SallingFoodWaste> log) : IFoodWasteSource
{
    public const int MaxPerDay = 90;
    private readonly object _gate = new();
    private int _calls;
    private DateOnly _day;

    public bool Enabled => key.Length > 10;

    public async Task<List<Clearance>> NearAsync(double lat, double lng, int km, CancellationToken ct)
    {
        var area = Clearances.Area(lat, lng, km);
        var cacheKey = $"fw|{area.Lat:F2}|{area.Lng:F2}|{area.Km}";
        if (cache.TryGetValue(cacheKey, out List<Clearance>? hit) && hit is not null) return hit;
        lock (_gate)
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            if (_day != today) { _day = today; _calls = 0; }
            if (_calls >= MaxPerDay)
            {
                if (cache.TryGetValue("stale|" + cacheKey, out List<Clearance>? old) && old is not null) return old;
                throw new FoodWasteQuotaException();
            }
            _calls++;
        }
        var url = "https://api.sallinggroup.com/v1/food-waste/?geo=" +
                  $"{area.Lat.ToString(CultureInfo.InvariantCulture)},{area.Lng.ToString(CultureInfo.InvariantCulture)}&radius={area.Km}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        List<Clearance> list;
        try
        {
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                log.LogWarning("Salling food-waste answered {Status}", (int)res.StatusCode);
                throw new SourceUnavailableException("Salling svarer ikke lige nu. Prøv igen om lidt.");
            }
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            list = Parse(doc.RootElement, clock.GetUtcNow());
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            log.LogWarning(e, "Salling food-waste failed");
            throw new SourceUnavailableException("Salling svarer ikke lige nu. Prøv igen om lidt.");
        }
        // Markdowns change during the day, but the quota is small: an hour, plus a stale copy for when it runs out.
        cache.Set(cacheKey, list, TimeSpan.FromMinutes(60));
        cache.Set("stale|" + cacheKey, list, TimeSpan.FromHours(30));
        return list;
    }

    internal static List<Clearance> Parse(JsonElement root, DateTimeOffset now)
    {
        var list = new List<Clearance>();
        if (root.ValueKind != JsonValueKind.Array) return list;
        foreach (var s in root.EnumerateArray())
        {
            if (!s.TryGetProperty("store", out var store) || !s.TryGetProperty("clearances", out var cl) || cl.ValueKind != JsonValueKind.Array) continue;
            var address = store.TryGetProperty("address", out var a) ? $"{Str(a, "street")}, {Str(a, "zip")} {Str(a, "city")}".Trim(' ', ',') : null;
            double? km = store.TryGetProperty("distance_km", out var dk) && dk.ValueKind == JsonValueKind.Number ? Math.Round(dk.GetDouble(), 1) : null;
            double? slat = null, slng = null;   // "coordinates": [longitude, latitude]
            if (store.TryGetProperty("coordinates", out var co) && co.ValueKind == JsonValueKind.Array && co.GetArrayLength() == 2)
            { slng = co[0].GetDouble(); slat = co[1].GetDouble(); }
            foreach (var c in cl.EnumerateArray())
            {
                if (!c.TryGetProperty("offer", out var o) || !c.TryGetProperty("product", out var p)) continue;
                // "Pizza>Færdigretter>Mejeri & køl>..." - the first step is the most precise kind.
                var cat = p.TryGetProperty("categories", out var cs) ? Str(cs, "da")?.Split('>')[0].Trim() : null;
                list.Add(new Clearance(Str(store, "name") ?? "", Clearances.Brand(Str(store, "brand") ?? ""), address,
                    Clearances.Readable(Str(p, "description") ?? ""), Num(o, "newPrice") ?? 0, Num(o, "originalPrice"),
                    Num(o, "percentDiscount"), Num(o, "stock"), Str(o, "stockUnit") is "each" ? "stk" : Str(o, "stockUnit"), When(o, "endTime"),
                    Str(p, "image"), When(o, "startTime"), When(o, "lastUpdate"), cat, Str(p, "ean") ?? Str(o, "ean"), km, slat, slng));
            }
        }
        return Clearances.ForSale(list, now);
    }

    private static DateTimeOffset? When(JsonElement o, string name) =>
        Str(o, name) is { } e && DateTimeOffset.TryParse(e, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal? Num(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : null;
}

/// <summary>Faste tilbud in one JSON file in the data folder. Written to a temp file first, so a crash mid-write never
/// leaves half a file.</summary>
public sealed class JsonFixedDealStore : IFixedDealStore
{
    private readonly string _file;
    private readonly object _gate = new();
    private readonly List<FixedDeal> _deals;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonFixedDealStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _file = Path.Combine(dataDir, "faste-tilbud.json");
        _deals = File.Exists(_file) ? JsonSerializer.Deserialize<List<FixedDeal>>(File.ReadAllText(_file), Json) ?? [] : [];
    }

    public IReadOnlyList<FixedDeal> All() { lock (_gate) return _deals.ToList(); }

    public void Add(FixedDeal deal) { lock (_gate) { _deals.Add(deal); Save(); } }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            var n = _deals.RemoveAll(d => d.Id == id);
            if (n > 0) Save();
            return n > 0;
        }
    }

    private void Save()
    {
        var tmp = _file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_deals, Json));
        File.Move(tmp, _file, overwrite: true);
    }
}

/// <summary>Postcode -> place, from OpenStreetMap's Nominatim (DAWA closed in 2026). Cached a day; Nominatim asks for
/// at most 1 request a second.</summary>
public sealed class NominatimPlaces(HttpClient http, IMemoryCache cache) : IPlaceLookup
{
    public async Task<Place?> FromPostcodeAsync(string postcode, CancellationToken ct)
    {
        postcode = new string(postcode.Where(char.IsDigit).ToArray());
        if (postcode.Length != 4) return null;
        if (cache.TryGetValue("p|" + postcode, out Place? hit)) return hit;
        var url = $"https://nominatim.openstreetmap.org/search?postalcode={postcode}&country=dk&format=json&limit=1&addressdetails=1";
        try
        {
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
            Place? place = null;
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var r = doc.RootElement[0];
                // A postcode search has no city, only the municipality: "Aarhus Kommune" -> "Aarhus".
                string? name = null;
                if (r.TryGetProperty("address", out var a))
                    foreach (var k in new[] { "city", "town", "village", "municipality" })
                        if (a.TryGetProperty(k, out var v) && v.GetString() is { Length: > 0 } x) { name = x.Replace(" Kommune", ""); break; }
                place = new Place($"{postcode} {name}".Trim(),
                    double.Parse(r.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture),
                    double.Parse(r.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture));
            }
            cache.Set("p|" + postcode, place, TimeSpan.FromDays(1));
            return place;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }
}
