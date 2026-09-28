using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ITMartinElPriser.Core;

public sealed record GridCompany(string Id, string Name, string Company, string PriceArea);

// Where the household is: a readable label, its postcode and the point used to find the
// ONE grid company that serves it (postcodes can be shared by two).
public sealed record Place(string Label, string PostalCode, double Lat, double Lon);

// Exact grid tariffs and the postcode -> grid company lookup, from Strømligning's open API
// (CC BY-NC 4.0: free, non-commercial ElPriser only, with attribution). Energinet's own
// DatahubPricelist has the same numbers but several codes, discounts and local tariffs per
// company; Strømligning has already resolved which one a household pays.
public sealed class GridTariffs(HttpClient http, ILogger<GridTariffs> logger)
{
    private const string Api = "https://stromligning.dk/api/";
    private readonly Dictionary<string, (Dictionary<DateTime, double> Slots, DateTime At)> _tariffs = new();
    private readonly Dictionary<string, (List<GridCompany> List, DateTime At)> _find = new();
    private readonly Dictionary<string, (List<(DateTime Hour, double Price)> List, DateTime At)> _forecast = new();
    private readonly object _lock = new();

    private const string Dawa = "https://api.dataforsyningen.dk/";

    // Address or postcode typed by the user -> one point. Dataforsyningen (the state's free
    // address register). A bare postcode uses its visual centre.
    public async Task<Place?> ResolveAsync(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return null;
        try
        {
            if (text.Length == 4 && text.All(char.IsDigit))
            {
                var pn = await http.GetFromJsonAsync<PostcodeDto>($"{Dawa}postnumre/{text}");
                return pn is { Visueltcenter.Length: 2 } ? new Place($"{pn.Nr} {pn.Navn}", pn.Nr, pn.Visueltcenter[1], pn.Visueltcenter[0]) : null;
            }
            var hits = await http.GetFromJsonAsync<List<AddressDto>>($"{Dawa}adresser?q={Uri.EscapeDataString(text)}&per_side=1&struktur=mini");
            return hits?.FirstOrDefault() is { } a ? new Place(a.Betegnelse, a.Postnr, a.Y, a.X) : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Address lookup failed for {Text}", text);
            return null;
        }
    }

    // The phone's location -> the nearest address, so the confirmation line is readable.
    public async Task<Place?> ReverseAsync(double lat, double lon)
    {
        try
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var a = await http.GetFromJsonAsync<AddressDto>($"{Dawa}adgangsadresser/reverse?x={lon.ToString(inv)}&y={lat.ToString(inv)}&struktur=mini");
            // DAWA always answers with the NEAREST Danish address - from Hamburg that is Bagenkop,
            // 130 km away. Further than 2 km from any Danish address = not in Denmark.
            if (a is null || (a.X != 0 && KmBetween(lat, lon, a.Y, a.X) > 2)) return null;
            return new Place(a.Betegnelse, a.Postnr, lat, lon);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Reverse lookup failed");
            return null;
        }
    }

    // The one grid company serving this exact point.
    public async Task<GridCompany?> FindAtAsync(double lat, double lon)
    {
        try
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var list = await http.GetFromJsonAsync<List<SupplierDto>>($"{Api}suppliers/find?lat={lat.ToString(inv)}&long={lon.ToString(inv)}") ?? [];
            return list.Select(s => new GridCompany(s.Id, ShortName(s.CompanyName), s.CompanyName, s.PriceArea)).FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Grid lookup failed at {Lat},{Lon}", lat, lon);
            return null;
        }
    }

    private sealed class PostcodeDto
    {
        [JsonPropertyName("nr")] public string Nr { get; set; } = "";
        [JsonPropertyName("navn")] public string Navn { get; set; } = "";
        [JsonPropertyName("visueltcenter")] public double[] Visueltcenter { get; set; } = [];
    }

    private static double KmBetween(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * r * Math.Asin(Math.Sqrt(h));
    }

    private sealed class AddressDto
    {
        [JsonPropertyName("betegnelse")] public string Betegnelse { get; set; } = "";
        [JsonPropertyName("postnr")] public string Postnr { get; set; } = "";
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
    }

    public async Task<List<GridCompany>> FindAsync(string postalCode)
    {
        postalCode = postalCode.Trim();
        if (postalCode.Length != 4 || !postalCode.All(char.IsDigit)) return [];
        lock (_lock) if (_find.TryGetValue(postalCode, out var c) && DateTime.UtcNow - c.At < TimeSpan.FromDays(7)) return c.List;
        try
        {
            var list = (await http.GetFromJsonAsync<List<SupplierDto>>($"{Api}suppliers/find?postalCode={postalCode}") ?? [])
                .Select(s => new GridCompany(s.Id, ShortName(s.CompanyName), s.CompanyName, s.PriceArea)).ToList();
            lock (_lock) _find[postalCode] = (list, DateTime.UtcNow);
            return list;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Grid lookup failed for {Postal}", postalCode);
            return [];
        }
    }

    // Gives the settings an exact tariff per quarter-hour. Anything outside the fetched
    // window (e.g. tomorrow before 13:00) uses the same time of day from the latest known day -
    // tariffs follow a fixed daily pattern within a season.
    public async Task AttachAsync(HouseholdSettings s)
    {
        if (string.IsNullOrEmpty(s.GridSupplierId)) { s.NettarifLookup = null; return; }
        var slots = await SlotsAsync(s.GridSupplierId);
        if (slots.Count == 0) { s.NettarifLookup = null; return; }
        var latestDay = slots.Keys.Max().Date;
        s.NettarifLookup = t =>
        {
            var q = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute / 15 * 15, 0);
            if (slots.TryGetValue(q, out var v)) return v;
            return slots.TryGetValue(latestDay.Add(q.TimeOfDay), out var same) ? same : null;
        };
    }

    // Today's tariff, hour by hour, incl. VAT in øre - for the "your grid company" card.
    public async Task<List<(int Hour, double OreInclVat)>> TodayAsync(string gridId)
    {
        var slots = await SlotsAsync(gridId);
        var today = DkTime.Now.Date;
        return [.. slots.Where(k => k.Key.Date == today)
            .GroupBy(k => k.Key.Hour).OrderBy(g => g.Key)
            .Select(g => (g.Key, Math.Round(g.Average(k => k.Value) * 100 * 1.25, 1)))];
    }

    private async Task<Dictionary<DateTime, double>> SlotsAsync(string gridId)
    {
        lock (_lock) if (_tariffs.TryGetValue(gridId, out var c) && DateTime.UtcNow - c.At < TimeSpan.FromHours(3)) return c.Slots;
        try
        {
            var from = DkTime.Now.Date.AddDays(-1).ToUniversalTime();
            var page = await http.GetFromJsonAsync<PricesDto>($"{Api}prices?supplierId={Uri.EscapeDataString(gridId)}&from={from:yyyy-MM-ddTHH:mm:ssZ}");
            var slots = new Dictionary<DateTime, double>();
            foreach (var p in page?.Prices ?? [])
                if (p.Details?.Distribution?.Value is { } v) slots[p.LocalDate] = v;
            if (slots.Count > 0) lock (_lock) _tariffs[gridId] = (slots, DateTime.UtcNow);
            return slots;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tariff fetch failed for {Grid}", gridId);
            lock (_lock) return _tariffs.TryGetValue(gridId, out var old) ? old.Slots : [];
        }
    }

    // Strømligning's price forecast for tomorrow, used only for its SHAPE ("cheapest around
    // 03") before Energinet publishes the real prices at ~13:00. Hourly, DK local time.
    public async Task<List<(DateTime Hour, double Price)>> TomorrowForecastAsync(string area)
    {
        lock (_lock) if (_forecast.TryGetValue(area, out var c) && DateTime.UtcNow - c.At < TimeSpan.FromHours(2)) return c.List;
        try
        {
            var tomorrow = DkTime.Now.Date.AddDays(1);
            var page = await http.GetFromJsonAsync<ForecastDto>($"{Api}forecasts/latest?priceArea={area}");
            var list = (page?.Prices ?? [])
                .Select(p => (Hour: DkTime.FromUtc(p.Datetime), p.Price))
                .Where(p => p.Hour.Date == tomorrow).ToList();
            lock (_lock) _forecast[area] = (list, DateTime.UtcNow);
            return list;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Forecast fetch failed for {Area}", area);
            return [];
        }
    }

    // "Konstant Net A/S - 151" -> "Konstant", "Radius Elnet A/S" -> "Radius".
    private static string ShortName(string company)
    {
        var n = company.Split(" - ")[0];
        foreach (var cut in new[] { " A/S", " A.m.b.a.", " AMBA", " A M B A", " Elnet", " El-net", " El-Net", " Net", " Elnet" })
            n = n.Replace(cut, "", StringComparison.OrdinalIgnoreCase);
        return n.Trim();
    }

    private sealed class SupplierDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("companyName")] public string CompanyName { get; set; } = "";
        [JsonPropertyName("priceArea")] public string PriceArea { get; set; } = "";
    }

    private sealed class PricesDto
    {
        [JsonPropertyName("prices")] public List<PriceRow> Prices { get; set; } = [];
    }

    private sealed class PriceRow
    {
        [JsonPropertyName("localDate")] public DateTime LocalDate { get; set; }
        [JsonPropertyName("details")] public Details? Details { get; set; }
    }

    private sealed class Details
    {
        [JsonPropertyName("distribution")] public Amount? Distribution { get; set; }
    }

    private sealed class Amount
    {
        [JsonPropertyName("value")] public double? Value { get; set; }
    }

    private sealed class ForecastDto
    {
        [JsonPropertyName("prices")] public List<ForecastRow> Prices { get; set; } = [];
    }

    private sealed class ForecastRow
    {
        [JsonPropertyName("datetime")] public DateTime Datetime { get; set; }
        [JsonPropertyName("price")] public double Price { get; set; }
    }
}
