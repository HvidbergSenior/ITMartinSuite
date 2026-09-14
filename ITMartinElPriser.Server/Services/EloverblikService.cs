using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITMartinElPriser.Server.Services;

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
    public async Task<List<HourReading>> GetHourlyAsync(string refreshToken, string meteringPointId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var token = await AccessTokenAsync(refreshToken, ct);
        // The API's dateTo is exclusive.
        var url = $"{Base}/meterdata/gettimeseries/{from:yyyy-MM-dd}/{to.AddDays(1):yyyy-MM-dd}/Hour";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new { meteringPoints = new { meteringPoint = new[] { meteringPointId } } });
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));

        var readings = new List<HourReading>();
        var dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        foreach (var r in doc.RootElement.GetProperty("result").EnumerateArray())
        {
            if (!r.TryGetProperty("MyEnergyData_MarketDocument", out var md)) continue;
            if (!md.TryGetProperty("TimeSeries", out var series)) continue;
            foreach (var ts in series.EnumerateArray())
            {
                if (!ts.TryGetProperty("Period", out var periods)) continue;
                foreach (var period in periods.EnumerateArray())
                {
                    var startUtc = DateTime.Parse(period.GetProperty("timeInterval").GetProperty("start").GetString()!,
                        null, System.Globalization.DateTimeStyles.AdjustToUniversal);
                    var resolution = period.TryGetProperty("resolution", out var resEl) ? resEl.GetString() : "PT1H";
                    var step = resolution == "PT15M" ? TimeSpan.FromMinutes(15) : TimeSpan.FromHours(1);
                    foreach (var pt in period.GetProperty("Point").EnumerateArray())
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

        // Quarter-hour meters: fold into hours so the rest of the app has one shape.
        return readings
            .GroupBy(r => new DateTime(r.HourDk.Year, r.HourDk.Month, r.HourDk.Day, r.HourDk.Hour, 0, 0))
            .Select(g => new HourReading(g.Key, Math.Round(g.Sum(r => r.Kwh), 3), g.All(r => r.Quality == "A04") ? "A04" : g.First().Quality))
            .OrderBy(r => r.HourDk)
            .ToList();
    }

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
