using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITMartinElPriser.Core;

namespace ITMartinMitEl.Server.Services;

// Reads the household's own meter through Energinet's Datahub customer
// API (eloverblik.dk). The user creates a personal "refresh token" on
// eloverblik.dk once; that token is exchanged for a 24-hour access token
// on every sync. Consumption arrives 1-2 days behind, hourly.
public sealed class EloverblikService(HttpClient http, ILogger<EloverblikService> logger)
{
    private const string Base = "https://api.eloverblik.dk/customerapi/api";

    private string? _accessToken;
    private DateTime _accessTokenExpires;
    private string? _accessTokenFor;

    public sealed record MeteringPoint(string Id, string Address, string TypeOfMp);
    public sealed record HourReading(DateTime HourDk, double Kwh, string Quality);

    private async Task<string> AccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        if (_accessToken is not null && _accessTokenFor == refreshToken && DateTime.UtcNow < _accessTokenExpires)
            return _accessToken;

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{Base}/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken.Trim());
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<TokenResponse>(ct);
        _accessToken = body?.Result ?? throw new InvalidOperationException("Eloverblik gav ingen adgangstoken");
        _accessTokenFor = refreshToken;
        _accessTokenExpires = DateTime.UtcNow.AddHours(12);
        return _accessToken;
    }

    public async Task<List<MeteringPoint>> GetMeteringPointsAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await AccessTokenAsync(refreshToken, ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{Base}/meteringpoints/meteringpoints?includeAll=false");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<MeteringPointsResponse>(ct);
        return (body?.Result ?? [])
            .Select(m => new MeteringPoint(m.MeteringPointId, $"{m.StreetName} {m.BuildingNumber}, {m.Postcode} {m.CityName}".Trim(), m.TypeOfMP))
            .ToList();
    }

    // Hourly kWh for [from, to] inclusive, Danish local hours.
    public async Task<List<HourReading>> GetHourlyAsync(string refreshToken, string meteringPointId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        FoldToHours(await GetSeriesAsync(refreshToken, meteringPointId, from, to, "Hour", ct));

    // kWh per quarter-hour (the meter's own resolution on a PT15M meter), Danish local time.
    // HourReading.HourDk is then the quarter's start.
    public Task<List<HourReading>> GetQuartersAsync(string refreshToken, string meteringPointId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        GetSeriesAsync(refreshToken, meteringPointId, from, to, "Quarter", ct);

    public static List<HourReading> FoldToHours(IEnumerable<HourReading> readings) => readings
        .GroupBy(r => new DateTime(r.HourDk.Year, r.HourDk.Month, r.HourDk.Day, r.HourDk.Hour, 0, 0))
        .Select(g => new HourReading(g.Key, Math.Round(g.Sum(r => r.Kwh), 3), g.All(r => r.Quality == "A04") ? "A04" : g.First().Quality))
        .OrderBy(r => r.HourDk)
        .ToList();

    private async Task<List<HourReading>> GetSeriesAsync(string refreshToken, string meteringPointId, DateOnly from, DateOnly to, string aggregation, CancellationToken ct)
    {
        var token = await AccessTokenAsync(refreshToken, ct);
        // The API's dateTo is exclusive.
        var url = $"{Base}/meterdata/gettimeseries/{from:yyyy-MM-dd}/{to.AddDays(1):yyyy-MM-dd}/{aggregation}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new { meteringPoints = new { meteringPoint = new[] { meteringPointId } } });
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));

        var readings = new List<HourReading>();
        var dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        string? apiError = null;
        foreach (var r in doc.RootElement.GetProperty("result").EnumerateArray())
        {
            if (r.TryGetProperty("errorText", out var et) && et.ValueKind == JsonValueKind.String)
                apiError = et.GetString();
            // A day with no data yet comes back as a result entry whose document
            // is null - skip it rather than crash the whole sync.
            if (!r.TryGetProperty("MyEnergyData_MarketDocument", out var md) || md.ValueKind != JsonValueKind.Object) continue;
            if (!md.TryGetProperty("TimeSeries", out var series) || series.ValueKind != JsonValueKind.Array) continue;
            foreach (var ts in series.EnumerateArray())
            {
                if (!ts.TryGetProperty("Period", out var periods) || periods.ValueKind != JsonValueKind.Array) continue;
                foreach (var period in periods.EnumerateArray())
                {
                    var startUtc = DateTime.Parse(period.GetProperty("timeInterval").GetProperty("start").GetString()!,
                        null, System.Globalization.DateTimeStyles.AdjustToUniversal);
                    var resolution = period.TryGetProperty("resolution", out var resEl) ? resEl.GetString() : "PT1H";
                    var step = resolution == "PT15M" ? TimeSpan.FromMinutes(15) : TimeSpan.FromHours(1);
                    if (!period.TryGetProperty("Point", out var points) || points.ValueKind != JsonValueKind.Array) continue;
                    foreach (var pt in points.EnumerateArray())
                    {
                        var pos = int.Parse(pt.GetProperty("position").GetString()!);
                        var qty = double.Parse(pt.GetProperty("out_Quantity.quantity").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                        var quality = pt.TryGetProperty("out_Quantity.quality", out var q) ? q.GetString() ?? "" : "";
                        var utc = startUtc + step * (pos - 1);
                        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), dk);
                        readings.Add(new HourReading(local, qty, quality));
                    }
                }
            }
        }

        // Datahub answers 200 with a per-meter error when the token holder is
        // not (or no longer) the registered customer on the meter. Say so.
        if (readings.Count == 0 && apiError is not null)
            throw new InvalidOperationException(apiError == "MeteringPointDataNotAvailableForTheRequestedPeriod"
                ? "Datahub har ingen data til dette token for måleren - typisk fordi elaftalen står i en andens navn. Opret tokenet med den persons MitID, eller giv en fuldmagt på eloverblik.dk."
                : $"Eloverblik: {apiError}");

        return readings.OrderBy(r => r.HourDk).ToList();
    }

    // Grid company, supplier and every charge Datahub lists on the meter, in one object.
    // Tariffs come per hour ("PT1H", 24 prices) or flat ("P1D", one price); rebates are
    // negative lines and are simply added in. Energinet's lines are told apart by owner GLN.
    public async Task<MeterCharges> GetMeterChargesAsync(string refreshToken, string meteringPointId, CancellationToken ct = default)
    {
        const string EnerginetGln = "5790000432752";
        var token = await AccessTokenAsync(refreshToken, ct);
        var body = new { meteringPoints = new { meteringPoint = new[] { meteringPointId } } };

        using var detailsDoc = await PostAsync($"{Base}/meteringpoints/meteringpoint/getdetails", body, token, ct);
        var d = detailsDoc.RootElement.GetProperty("result")[0].GetProperty("result");
        using var chargesDoc = await PostAsync($"{Base}/meteringpoints/meteringpoint/getcharges", body, token, ct);
        var c = chargesDoc.RootElement.GetProperty("result")[0].GetProperty("result");

        var m = new MeterCharges
        {
            GridOperatorName = Str(d, "gridOperatorName"),
            SupplierName = Str(d, "balanceSupplierName"),
            MeterReadingOccurrence = Str(d, "meterReadingOccurrence"),
            FetchedUtc = DateTime.UtcNow,
        };
        var net = new double[24];
        var hasNet = false;
        if (c.TryGetProperty("tariffs", out var tariffs) && tariffs.ValueKind == JsonValueKind.Array)
            foreach (var t in tariffs.EnumerateArray())
            {
                var prices = t.TryGetProperty("prices", out var p) && p.ValueKind == JsonValueKind.Array
                    ? p.EnumerateArray().Select(x => Num(x, "price")).ToList() : [];
                if (prices.Count == 0) continue;
                var name = Str(t, "name");
                if (Str(t, "owner") == EnerginetGln)
                {
                    if (name.Contains("afgift", StringComparison.OrdinalIgnoreCase)) m.ElafgiftKrPerKwh += prices[0];
                    else m.EnerginetKrPerKwh += prices[0];
                    continue;
                }
                hasNet = true;
                for (var h = 0; h < 24; h++) net[h] += prices.Count >= 24 ? prices[h] : prices[0];
            }
        if (hasNet) m.NetTariffByHour = [.. net.Select(x => Math.Round(x, 5))];
        if (c.TryGetProperty("subscriptions", out var subs) && subs.ValueKind == JsonValueKind.Array)
            foreach (var sub in subs.EnumerateArray()) m.SubscriptionsKrPerMonth += Num(sub, "price");
        m.SubscriptionsKrPerMonth = Math.Round(m.SubscriptionsKrPerMonth, 2);
        return m;
    }

    private async Task<JsonDocument> PostAsync(string url, object body, string token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(body);
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static double Num(JsonElement e, string name) =>
        !e.TryGetProperty(name, out var v) ? 0
        : v.ValueKind == JsonValueKind.Number ? v.GetDouble()
        : v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ? x
        : 0;

    private sealed record TokenResponse([property: JsonPropertyName("result")] string? Result);
    private sealed record MeteringPointsResponse([property: JsonPropertyName("result")] List<MpDto>? Result);
    private sealed record MpDto(
        [property: JsonPropertyName("meteringPointId")] string MeteringPointId,
        [property: JsonPropertyName("typeOfMP")] string TypeOfMP,
        [property: JsonPropertyName("streetName")] string StreetName,
        [property: JsonPropertyName("buildingNumber")] string BuildingNumber,
        [property: JsonPropertyName("postcode")] string Postcode,
        [property: JsonPropertyName("cityName")] string CityName);
}
