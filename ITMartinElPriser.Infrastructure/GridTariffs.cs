using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

using ITMartinElPriser.Application;
using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Infrastructure;

// Where the household is and which grid tariff it pays.
// - Address <-> point: OpenStreetMap Nominatim (below).
// - Grid company at an address: Green Power Denmark's "Find Netselskab" API (elnet.dk; returns name + area
//   number "def", e.g. 151 = Konstant). Terms not published - permission asked 2026-11-24.
// - Tariffs: Energinet DatahubPricelist (CC BY 4.0, "Kilde: Energinet"), fetched ONCE a day for every company
//   (the API answers 429 after ~2 calls a minute), see DatahubTariffBook.
// Strømligning (CC BY-NC, no commercial use) was dropped 2026-11-24.
public sealed class GridTariffs(HttpClient http, ILogger<GridTariffs> logger) : IGridTariffSource
{
    private const string Gpd = "https://api.elnet.greenpowerdenmark.dk/api/supplierlookup/";
    private const string Eds = "https://api.energidataservice.dk/dataset/DatahubPricelist";
    private readonly Dictionary<string, (GridCompany? Grid, DateTime At)> _found = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _bookGate = new(1, 1);
    private DatahubTariffBook? _book;
    private DateTime _bookAt = DateTime.MinValue, _bookTried = DateTime.MinValue;

    // DAWA (api.dataforsyningen.dk) was switched off for good - it answers 410 Gone since 2026.
    // Addresses now come from OpenStreetMap's Nominatim: free, no key, Danish addresses are
    // complete in OSM. Its rules: a real User-Agent, at most 1 request a second (one lookup per
    // visitor action is far below that), and the "© OpenStreetMap" credit shown in the app.
    private const string Osm = "https://nominatim.openstreetmap.org/";
    private const string UserAgent = "ITMartinElPriser/1.0 (ITMartin@Mensa.dk)";
    private static readonly SemaphoreSlim OsmGate = new(1, 1);
    private static DateTime _lastOsm = DateTime.MinValue;

    // Address or postcode typed by the user -> one point. A bare postcode uses the area's centre.
    public async Task<Place?> ResolveAsync(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return null;
        try
        {
            var query = text.Length == 4 && text.All(char.IsDigit)
                ? $"postalcode={text}"
                : $"q={Uri.EscapeDataString(text)}";
            var hits = await OsmAsync<List<OsmPlace>>($"search?{query}&countrycodes=dk&format=jsonv2&addressdetails=1&limit=1");
            if (hits?.FirstOrDefault() is not { Address: { } addr } h) return null;
            // A postcode search names only the municipality ("8000, Aarhus Kommune"). The town the
            // postcode belongs to ("8000 Aarhus", "9990 Skagen") comes from the address at its centre.
            if (addr.Town() == "")
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var at = await OsmAsync<OsmPlace>($"reverse?lat={h.LatD.ToString(inv)}&lon={h.LonD.ToString(inv)}&format=jsonv2&addressdetails=1&zoom=18");
                addr.City = at?.Address is { } r && r.Postcode == addr.Postcode && r.Town() != "" ? r.Town() : addr.MunicipalityTown();
            }
            return ToPlace(h, h.LatD, h.LonD);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Address lookup failed for {Text}", text);
            return null;
        }
    }

    // The phone's location -> the nearest address, so the confirmation line is readable.
    // Outside Denmark (country code not dk) there is no Danish grid company to find.
    public async Task<Place?> ReverseAsync(double lat, double lon)
    {
        try
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var h = await OsmAsync<OsmPlace>($"reverse?lat={lat.ToString(inv)}&lon={lon.ToString(inv)}&format=jsonv2&addressdetails=1&zoom=18");
            if (h?.Address is not { CountryCode: "dk" }) return null;
            return ToPlace(h, lat, lon);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Reverse lookup failed");
            return null;
        }
    }

    internal static Place? ToPlace(OsmPlace h, double lat, double lon)
    {
        var a = h.Address;
        if (a is null || string.IsNullOrEmpty(a.Postcode)) return null;
        var town = a.Town();
        var street = string.Join(' ', new[] { a.Road, a.HouseNumber }.Where(x => !string.IsNullOrEmpty(x)));
        var area = string.Join(' ', new[] { a.Postcode, town }.Where(x => !string.IsNullOrEmpty(x)));
        var label = street == "" ? area : $"{street}, {area}";
        return new Place(label, a.Postcode, lat, lon, a.Region ?? "");
    }

    private async Task<T?> OsmAsync<T>(string path)
    {
        await OsmGate.WaitAsync();
        try
        {
            var wait = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - _lastOsm);
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
            using var req = new HttpRequestMessage(HttpMethod.Get, Osm + path);
            req.Headers.UserAgent.ParseAdd(UserAgent);
            req.Headers.AcceptLanguage.ParseAdd("da");
            using var res = await http.SendAsync(req);
            _lastOsm = DateTime.UtcNow;
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadFromJsonAsync<T>();
        }
        finally { OsmGate.Release(); }
    }

    internal sealed class OsmPlace
    {
        [JsonPropertyName("lat")] public string Lat { get; set; } = "";
        [JsonPropertyName("lon")] public string Lon { get; set; } = "";
        [JsonPropertyName("address")] public OsmAddress? Address { get; set; }
        public double LatD => double.Parse(Lat, System.Globalization.CultureInfo.InvariantCulture);
        public double LonD => double.Parse(Lon, System.Globalization.CultureInfo.InvariantCulture);
    }

    internal sealed class OsmAddress
    {
        [JsonPropertyName("road")] public string? Road { get; set; }
        [JsonPropertyName("house_number")] public string? HouseNumber { get; set; }
        [JsonPropertyName("postcode")] public string? Postcode { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("town")] public string? TownName { get; set; }
        [JsonPropertyName("village")] public string? Village { get; set; }
        [JsonPropertyName("suburb")] public string? Suburb { get; set; }
        [JsonPropertyName("municipality")] public string? Municipality { get; set; }
        [JsonPropertyName("country_code")] public string? CountryCode { get; set; }
        [JsonPropertyName("ISO3166-2-lvl4")] public string? Region { get; set; }
        public string Town() => City ?? TownName ?? Village ?? Suburb ?? "";

        // "Aarhus Kommune" -> "Aarhus", "Bornholms Regionskommune" -> "Bornholm": the fallback town name.
        public string? MunicipalityTown()
        {
            var m = Municipality?.Trim();
            if (string.IsNullOrEmpty(m)) return null;
            foreach (var end in new[] { "s Regionskommune", " Regionskommune", " Kommune" })
                if (m.EndsWith(end, StringComparison.Ordinal)) { m = m[..^end.Length]; break; }
            // The one municipality whose name is a genitive of its town.
            return m == "Københavns" ? "København" : m;
        }
    }

    // The one grid company serving this place. Green Power Denmark looks up a street address, so a bare
    // postcode ("8000 Aarhus") is first turned into the address at its centre.
    public async Task<GridCompany?> FindAtAsync(Place place)
    {
        var label = place.Label;
        if (!label.Contains(',') && await ReverseAsync(place.Lat, place.Lon) is { } at) label = at.Label;
        lock (_lock) if (_found.TryGetValue(label, out var c) && DateTime.UtcNow - c.At < TimeSpan.FromDays(30)) return c.Grid;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Gpd + Uri.EscapeDataString(label));
            req.Headers.UserAgent.ParseAdd("ITMartinElPriser/1.0");
            using var res = await http.SendAsync(req);
            res.EnsureSuccessStatusCode();
            var hit = await res.Content.ReadFromJsonAsync<GpdDto>();
            GridCompany? grid = null;
            if (hit is { Def.Length: > 0, Name.Length: > 0 })
            {
                var book = await BookAsync();
                var owner = book?.OwnerFor(hit.Def, hit.Name) ?? book?.OwnerForShortName(ShortName(hit.Name));
                // Display name from Energinet's spelling ("Konstant"), not Green Power Denmark's ("KONSTANT").
                grid = new GridCompany(owner ?? $"Netområde {hit.Def}", ShortName(owner ?? hit.Name), hit.Name, AreaOf(place));
            }
            lock (_lock) _found[label] = (grid, DateTime.UtcNow);
            return grid;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Grid lookup failed for {Label}", label);
            return null;
        }
    }

    // Bornholm, Zealand, Lolland-Falster and the islands around them (Region Hovedstaden DK-84 and Sjælland DK-85)
    // are price area DK2; Jutland and Funen (incl. Samsø, Ærø, Anholt) are DK1.
    internal static string AreaOf(Place p) => p.Region is "DK-84" or "DK-85" ? "DK2" : "DK1";

    // Gives the settings the exact grid tariff per hour (kr/kWh ex VAT).
    public async Task AttachAsync(HouseholdSettings s)
    {
        s.NettarifLookup = null;
        if (string.IsNullOrEmpty(s.GridSupplierId) || await BookAsync() is not { } book) return;
        // "Netområde 151" = found while the list was still loading: resolve it now by area number or name.
        var owner = book.Has(s.GridSupplierId) ? s.GridSupplierId
            : s.GridSupplierId.StartsWith("Netområde ", StringComparison.Ordinal)
                ? book.OwnerFor(s.GridSupplierId["Netområde ".Length..], s.GridName) ?? book.OwnerForShortName(s.GridName)
            : GridIds.IsLegacy(s.GridSupplierId) ? book.OwnerForShortName(s.GridName) : null;
        if (owner is null) return;
        s.NettarifLookup = t => book.At(owner, t);
    }

    // Today's tariff, hour by hour, incl. VAT in øre - for the "your grid company" card.
    public async Task<List<(int Hour, double OreInclVat)>> TodayAsync(string gridId)
    {
        var s = new HouseholdSettings { GridSupplierId = gridId };
        await AttachAsync(s);
        if (s.NettarifLookup is not { } look) return [];
        var today = DkTime.Now.Date;
        var list = new List<(int Hour, double OreInclVat)>();
        for (var h = 0; h < 24; h++)
            if (look(today.AddHours(h)) is { } kr) list.Add((h, Math.Round(kr * 100 * 1.25, 1)));
        return list;
    }

    // The whole household tariff list, at most once a day for everyone. Two calls: every grid tariff row from the
    // last 13 months (rebates and seasonal rows), plus the base "Nettarif C" rows of any age (some companies have
    // not changed theirs for years). On failure the old book is kept and a new try waits 10 minutes.
    /// <summary>Starts loading the tariff list in the background (called at start-up): a full load takes about a
    /// minute because of Energi Data Service's rate limit, and no visitor should wait for that.</summary>
    public void Warm() => _ = Task.Run(LoadAsync);

    /// <summary>For the live test: waits (at most max) until the tariff list is loaded.</summary>
    internal async Task<bool> WaitLoadedAsync(TimeSpan max)
    {
        var until = DateTime.UtcNow + max;
        Warm();
        while (_book is null && DateTime.UtcNow < until) await Task.Delay(1000);
        return _book is not null;
    }

    private async Task<DatahubTariffBook?> BookAsync()
    {
        if (_book is { } b)
        {
            // Stale after a day: refresh in the background, keep answering with the old list meanwhile.
            if (DateTime.UtcNow - _bookAt >= TimeSpan.FromHours(24) && DateTime.UtcNow - _bookTried >= TimeSpan.FromMinutes(10))
                _ = Task.Run(LoadAsync);
            return b;
        }
        // Not loaded yet (just started; Energi Data Service's rate limit can make it take minutes): NEVER make a page wait
        // for it (2026-11-24: the front page spun after a deploy). Start the load if it is not running and answer without
        // the exact tariff - the standard bands are used until it is there.
        if (_bookGate.CurrentCount > 0) _ = Task.Run(LoadAsync);
        await Task.CompletedTask;
        return null;
    }

    private async Task<DatahubTariffBook?> LoadAsync()
    {
        if (DateTime.UtcNow - _bookTried < TimeSpan.FromMinutes(10) && _bookGate.CurrentCount > 0) return _book;
        await _bookGate.WaitAsync();
        try
        {
            if (_book is { } again && DateTime.UtcNow - _bookAt < TimeSpan.FromHours(24)) return again;
            _bookTried = DateTime.UtcNow;
            const string cols = "&columns=ChargeOwner,ChargeTypeCode,Note,ValidFrom,ValidTo,Price1,Price2,Price3,Price4,Price5,Price6,Price7,Price8,Price9,Price10,Price11,Price12,Price13,Price14,Price15,Price16,Price17,Price18,Price19,Price20,Price21,Price22,Price23,Price24";
            var since = DkTime.Now.Date.AddMonths(-13).ToString("yyyy-MM-dd");
            var d03 = Uri.EscapeDataString("{\"ChargeType\":[\"D03\"]}");
            var baseNotes = Uri.EscapeDataString("{\"ChargeType\":[\"D03\"],\"Note\":[\"Nettarif C\",\"Nettarif C time\",\"Nettarif C Time\"]}");
            var recent = await EdsAsync($"{Eds}?limit=200000&start={since}{cols}&filter={d03}");
            var bases = await EdsAsync($"{Eds}?limit=20000{cols}&filter={baseNotes}");
            var rows = DatahubTariffBook.Household(recent.Concat(bases))
                .DistinctBy(r => (r.Owner, r.Code, r.From)).ToList();
            if (rows.Count == 0) throw new InvalidOperationException("DatahubPricelist gave no household tariffs");
            _book = new DatahubTariffBook(rows);
            _bookAt = DateTime.UtcNow;
            logger.LogInformation("Grid tariffs: {Rows} rows for {Owners} grid companies (Energinet DatahubPricelist)", rows.Count, _book.Owners.Count);
            return _book;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Grid tariff list fetch failed - keeping the old one");
            return _book;
        }
        finally { _bookGate.Release(); }
    }

    // Energi Data Service limits calls hard (ElPriser and MinElpris share one IP): up to three 429s are waited out
    // (the message says how long, max 70 s each).
    private async Task<List<DatahubTariffBook.Record>> EdsAsync(string url)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var res = await http.GetAsync(url);
            if ((int)res.StatusCode == 429 && attempt < 3)
            {
                var text = await res.Content.ReadAsStringAsync();
                var m = System.Text.RegularExpressions.Regex.Match(text, @"(\d+) second");
                var secs = m.Success ? int.Parse(m.Groups[1].Value) : 60;
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(secs + 2, 70)));
                continue;
            }
            res.EnsureSuccessStatusCode();
            var page = await res.Content.ReadFromJsonAsync<EdsPage>();
            return page?.Records ?? [];
        }
    }

    private sealed class EdsPage
    {
        [JsonPropertyName("records")] public List<DatahubTariffBook.Record> Records { get; set; } = [];
    }

    private sealed class GpdDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("def")] public string Def { get; set; } = "";
    }

    // "Konstant Net A/S - 151" -> "Konstant", "Radius Elnet A/S" -> "Radius".
    private static string ShortName(string company)
    {
        var n = company.Split(" - ")[0];
        foreach (var cut in new[] { " A/S", " A.m.b.a.", " AMBA", " A M B A", " Elnet", " El-net", " El-Net", " Net", " Elnet" })
            n = n.Replace(cut, "", StringComparison.OrdinalIgnoreCase);
        return n.Trim();
    }
}
