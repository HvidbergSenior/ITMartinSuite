using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace ITMartinTilbud.Server.Services;

// One item marked down because its date is near ("madspild"), in one store.
public sealed record Clearance(
    string Store, string Brand, string? Address, string Title, decimal NewPrice, decimal? OriginalPrice,
    decimal? PercentDiscount, decimal? Stock, string? StockUnit, DateTimeOffset? EndTime, string? Image);

// Salling Group's OFFICIAL Anti Food Waste API (free key from developer.sallinggroup.dev): Netto, føtex and Bilka mark
// items down near their date. Key in Tilbud__SallingKey; without it the feature is simply not shown.
// Field names as published by Salling (store.name/brand/address, clearances[].offer.newPrice/originalPrice/percentDiscount/
// stock/stockUnit/endTime, clearances[].product.description/image) - not yet checked against a live answer (no key yet).
public sealed class FoodWasteQuotaException : Exception;

public sealed class SallingFoodWaste(HttpClient http, IMemoryCache cache, IConfiguration cfg, ILogger<SallingFoodWaste> log)
{
    private string Key => cfg["Tilbud:SallingKey"] ?? "";
    public bool Enabled => Key.Length > 10;

    // Salling allows only 100 requests a day for this API. So: the place is rounded to ~5 km squares and the radius to
    // 5/10/20 km, an answer is reused for an hour, and after MaxPerDay calls the last answer for that square is shown.
    public const int MaxPerDay = 90;
    private int _calls;
    private DateOnly _day;
    private readonly object _gate = new();

    public async Task<List<Clearance>> NearAsync(double lat, double lng, int km, CancellationToken ct)
    {
        km = km <= 5 ? 5 : km <= 10 ? 10 : 20;
        lat = Math.Round(lat * 20) / 20; lng = Math.Round(lng * 20) / 20;   // 0.05° ≈ 5 km
        var key = $"fw|{lat:F2}|{lng:F2}|{km}";
        if (cache.TryGetValue(key, out List<Clearance>? hit) && hit is not null) return hit;
        lock (_gate)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (_day != today) { _day = today; _calls = 0; }
            if (_calls >= MaxPerDay)
            {
                if (cache.TryGetValue("stale|" + key, out List<Clearance>? old) && old is not null) return old;
                throw new FoodWasteQuotaException();
            }
            _calls++;
        }
        var url = "https://api.sallinggroup.com/v1/food-waste/?geo=" +
                  $"{lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}&radius={km}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        List<Clearance> list;
        try
        {
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                log.LogWarning("Salling food-waste answered {Status}", (int)res.StatusCode);
                throw new OffersUnavailableException();
            }
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            list = Parse(doc.RootElement, DateTimeOffset.UtcNow);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            log.LogWarning(e, "Salling food-waste failed");
            throw new OffersUnavailableException();
        }
        // Markdowns change during the day, but the quota is small: an hour, plus a stale copy for when it runs out.
        cache.Set(key, list, TimeSpan.FromMinutes(60));
        cache.Set("stale|" + key, list, TimeSpan.FromHours(30));
        return list;
    }

    internal static List<Clearance> Parse(JsonElement root, DateTimeOffset now)
    {
        var list = new List<Clearance>();
        if (root.ValueKind != JsonValueKind.Array) return list;
        foreach (var s in root.EnumerateArray())
        {
            if (!s.TryGetProperty("store", out var store) || !s.TryGetProperty("clearances", out var cl) || cl.ValueKind != JsonValueKind.Array) continue;
            var name = Str(store, "name") ?? "";
            var brand = Str(store, "brand") ?? "";
            var address = store.TryGetProperty("address", out var a) ? $"{Str(a, "street")}, {Str(a, "zip")} {Str(a, "city")}".Trim(' ', ',') : null;
            foreach (var c in cl.EnumerateArray())
            {
                if (!c.TryGetProperty("offer", out var o) || !c.TryGetProperty("product", out var p)) continue;
                var end = Str(o, "endTime") is { } e && DateTimeOffset.TryParse(e, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTimeOffset?)null;
                if (end is { } t && t < now) continue;
                if (Num(o, "newPrice") is not { } price || price <= 0) continue;
                if (Num(o, "stock") is { } st && st <= 0) continue;
                list.Add(new Clearance(name, Pretty(brand), address, Str(p, "description") ?? "", price, Num(o, "originalPrice"),
                    Num(o, "percentDiscount"), Num(o, "stock"), Str(o, "stockUnit") is "each" ? "stk" : Str(o, "stockUnit"), end, Str(p, "image")));
            }
        }
        return list.OrderByDescending(x => x.PercentDiscount ?? 0).ToList();
    }

    private static string Pretty(string brand) => brand.ToLowerInvariant() switch
    {
        "netto" => "Netto", "foetex" or "føtex" => "føtex", "bilka" => "Bilka", "salling" => "Salling", _ => brand,
    };

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal? Num(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : null;
}
