using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ITMartinTemperatur.Application;
using ITMartinTemperatur.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinTemperatur.Infrastructure;

// DMI open data (no key since the move to opendataapi.dmi.dk, checked 2026-10-15): every station's dry-bulb
// temperature in a box, the last hour, plus the station names (cached for a day).
public sealed class DmiSource(IHttpClientFactory http) : IStationSource
{
    private const string Base = "https://opendataapi.dmi.dk/v2/metObs/collections";
    private Dictionary<string, string> _names = [];
    private DateTime _namesAt;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string Name => "DMI";
    public Area Covers => new(54.4, 7.8, 57.9, 15.4);

    public async Task<IReadOnlyList<Reading>> NearAsync(GeoPoint here, double radiusKm, CancellationToken ct)
    {
        var box = Area.Around(here, radiusKm);
        var bbox = string.Join(',', new[] { box.MinLon, box.MinLat, box.MaxLon, box.MaxLat }.Select(F));
        var client = http.CreateClient("weather");
        // An explicit time window, not period=latest-hour: that shortcut leaves about half the stations out
        // (Kastrup among them - checked 2026-10-15: 48 vs 96 stations). The answer comes in pages; all are read.
        var since = System.DateTime.UtcNow.AddMinutes(-70).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        string? url = $"{Base}/observation/items?parameterId=temp_dry&datetime={since}/..&bbox={bbox}&limit=10000";
        var names = await NamesAsync(ct);
        var list = new List<Reading>();
        for (var page = 0; url is not null && page < 10; page++)
        {
            using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
            foreach (var f in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                var p = f.GetProperty("properties");
                var c = f.GetProperty("geometry").GetProperty("coordinates");
                var id = p.GetProperty("stationId").GetString() ?? "";
                list.Add(new Reading(names.GetValueOrDefault(id, "DMI " + id),
                    new GeoPoint(c[1].GetDouble(), c[0].GetDouble()),
                    p.GetProperty("value").GetDouble(),
                    p.GetProperty("observed").GetDateTimeOffset(), Name, true));
            }
            url = doc.RootElement.TryGetProperty("links", out var links)
                ? links.EnumerateArray().Where(l => l.GetProperty("rel").GetString() == "next")
                    .Select(l => l.GetProperty("href").GetString()).FirstOrDefault()
                : null;
        }
        return list;
    }

    private async Task<Dictionary<string, string>> NamesAsync(CancellationToken ct)
    {
        if (_names.Count > 0 && DateTime.UtcNow - _namesAt < TimeSpan.FromDays(1)) return _names;
        await _gate.WaitAsync(ct);
        try
        {
            if (_names.Count > 0 && DateTime.UtcNow - _namesAt < TimeSpan.FromDays(1)) return _names;
            using var doc = JsonDocument.Parse(await http.CreateClient("weather").GetStringAsync(
                $"{Base}/station/items?status=Active&limit=10000", ct));
            var names = new Dictionary<string, string>();
            foreach (var f in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                var p = f.GetProperty("properties");
                if (p.GetProperty("stationId").GetString() is { } id && p.GetProperty("name").GetString() is { } n)
                    names[id] = n;
            }
            _names = names;
            _namesAt = DateTime.UtcNow;
            return names;
        }
        finally { _gate.Release(); }
    }

    private static string F(double d) => d.ToString("0.####", CultureInfo.InvariantCulture);
}

// The German weather service (DWD) through Bright Sky (free, no key): the nearest station with a live (SYNOP)
// measurement inside the radius.
public sealed class DwdSource(IHttpClientFactory http) : IStationSource
{
    public string Name => "DWD";
    public Area Covers => new(47.2, 5.8, 55.1, 15.1);

    public async Task<IReadOnlyList<Reading>> NearAsync(GeoPoint here, double radiusKm, CancellationToken ct)
    {
        var url = $"https://api.brightsky.dev/current_weather?lat={F(here.Lat)}&lon={F(here.Lon)}&max_dist={(int)(radiusKm * 1000)}";
        using var res = await http.CreateClient("weather").GetAsync(url, ct);
        if (!res.IsSuccessStatusCode) return [];   // 404 = no station inside the radius
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var w = doc.RootElement.GetProperty("weather");
        if (w.GetProperty("temperature").ValueKind != JsonValueKind.Number) return [];
        var src = doc.RootElement.GetProperty("sources").EnumerateArray()
            .FirstOrDefault(s => s.GetProperty("id").GetInt64() == w.GetProperty("source_id").GetInt64());
        if (src.ValueKind != JsonValueKind.Object) return [];
        return [new Reading(src.GetProperty("station_name").GetString() ?? "DWD",
            new GeoPoint(src.GetProperty("lat").GetDouble(), src.GetProperty("lon").GetDouble()),
            w.GetProperty("temperature").GetDouble(), w.GetProperty("timestamp").GetDateTimeOffset(), Name, true)];
    }

    private static string F(double d) => d.ToString("0.####", CultureInfo.InvariantCulture);
}

// Spain: AEMET OpenData (free key, Temperatur__AemetKey; the key from 2026-10-15 runs to 2027-01-23). One request gives a
// link to ~10,000 rows - the last 12 hours of every station - so the whole package is kept for 10 minutes and shared.
public sealed class AemetSource(IHttpClientFactory http, string apiKey) : IStationSource
{
    private IReadOnlyList<Reading> _all = [];
    private System.DateTime _at;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string Name => "AEMET";
    public Area Covers => new(27.4, -18.5, 44.0, 4.6);   // mainland, Balearics and the Canaries

    public async Task<IReadOnlyList<Reading>> NearAsync(GeoPoint here, double radiusKm, CancellationToken ct)
    {
        var all = await AllAsync(ct);
        return all.Where(r => here.KmTo(r.Point) <= radiusKm).ToList();
    }

    private async Task<IReadOnlyList<Reading>> AllAsync(CancellationToken ct)
    {
        if (System.DateTime.UtcNow - _at < TimeSpan.FromMinutes(10)) return _all;
        await _gate.WaitAsync(ct);
        try
        {
            if (System.DateTime.UtcNow - _at < TimeSpan.FromMinutes(10)) return _all;
            var client = http.CreateClient("weather");
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://opendata.aemet.es/opendata/api/observacion/convencional/todas");
            req.Headers.Add("api_key", apiKey);
            using var res = await client.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            // AEMET labels both answers "ISO-8859-15", which .NET does not know by that name - read the bytes as Latin-1.
            using var meta = JsonDocument.Parse(System.Text.Encoding.Latin1.GetString(await res.Content.ReadAsByteArrayAsync(ct)));
            var link = meta.RootElement.GetProperty("datos").GetString()!;
            // The data file is ISO-8859-15 (Spanish letters), not UTF-8.
            var bytes = await client.GetByteArrayAsync(link, ct);
            var text = System.Text.Encoding.Latin1.GetString(bytes);
            using var doc = JsonDocument.Parse(text);
            var list = new List<Reading>();
            foreach (var r in doc.RootElement.EnumerateArray())
            {
                if (!r.TryGetProperty("ta", out var ta) || ta.ValueKind != JsonValueKind.Number) continue;
                // "2026-10-15T14:00:00+0000" - UTC, written without the colon .NET's zzz expects.
                var fint = (r.GetProperty("fint").GetString() ?? "").Replace("+0000", "");
                if (!DateTimeOffset.TryParseExact(fint, "yyyy-MM-dd'T'HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)) continue;
                list.Add(new Reading(Title(r.GetProperty("ubi").GetString() ?? ""),
                    new GeoPoint(r.GetProperty("lat").GetDouble(), r.GetProperty("lon").GetDouble()),
                    ta.GetDouble(), at, Name, true));
            }
            _all = list;
            _at = System.DateTime.UtcNow;
            return _all;
        }
        finally { _gate.Release(); }
    }

    // "MÁLAGA  PUERTO  OCEANOGRÁFICO" -> "Málaga Puerto Oceanográfico"
    private static string Title(string s) =>
        string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length <= 2 && w.All(char.IsLetter) && w != w.ToUpperInvariant() ? w
                : char.ToUpper(w[0]) + w[1..].ToLowerInvariant()));
}

// Open-Meteo (no key, free for non-commercial use): a weather model's value for the exact spot. Shown as calculated.
public sealed class OpenMeteoModel(IHttpClientFactory http) : IModelSource
{
    public async Task<Reading?> AtAsync(GeoPoint here, CancellationToken ct)
    {
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={F(here.Lat)}&longitude={F(here.Lon)}&current=temperature_2m&timezone=UTC";
        var r = await http.CreateClient("weather").GetFromJsonAsync<OmResponse>(url, ct);
        if (r?.Current is not { } c) return null;
        // Open-Meteo gives "2026-10-15T14:00" in UTC (timezone=UTC), without an offset.
        var at = DateTimeOffset.ParseExact(c.Time, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        return new Reading("Beregnet for dit sted", here, c.Temperature_2m, at, "Open-Meteo", false);
    }

    private static string F(double d) => d.ToString("0.####", CultureInfo.InvariantCulture);

    private sealed record OmResponse(OmCurrent? Current);
    private sealed record OmCurrent(string Time, double Temperature_2m);
}

// Place names to positions (Open-Meteo geocoding, no key) - Danish names for countries.
public sealed class OpenMeteoPlaces(IHttpClientFactory http) : IPlaceSearch
{
    public async Task<IReadOnlyList<Place>> FindAsync(string name, CancellationToken ct)
    {
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(name)}&count=6&language=da&format=json";
        using var doc = JsonDocument.Parse(await http.CreateClient("weather").GetStringAsync(url, ct));
        if (!doc.RootElement.TryGetProperty("results", out var results)) return [];
        string S(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
        return results.EnumerateArray()
            .Select(r => new Place(S(r, "name"), S(r, "admin1"), S(r, "country"),
                new GeoPoint(r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble())))
            .ToList();
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddTemperaturInfrastructure(this IServiceCollection services, string aemetKey = "")
    {
        services.AddHttpClient("weather", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(12);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("ITMartinTemperatur/1.0 (ITMartin@Mensa.dk)");
        });
        services.AddSingleton<IStationSource, DmiSource>();
        services.AddSingleton<IStationSource, DwdSource>();
        // Spain only when a key is configured; without it Spain falls back to the calculated value.
        if (aemetKey.Length > 20)
            services.AddSingleton<IStationSource>(sp => new AemetSource(sp.GetRequiredService<IHttpClientFactory>(), aemetKey));
        services.AddSingleton<IModelSource, OpenMeteoModel>();
        services.AddSingleton<IPlaceSearch, OpenMeteoPlaces>();
        return services;
    }
}
