using System.Globalization;
using System.Text.Json;
using ITMartinTilbud.Application;
using ITMartinTilbud.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ITMartinTilbud.Infrastructure;

/// <summary>Weekly offers from Tjek (the company behind eTilbudsavis): one search covers Lidl, Netto, REMA 1000, føtex,
/// Bilka, Kvickly, SuperBrugsen, MENY, Løvbjerg, SPAR, 365discount and more. No key. Unofficial API - ask Tjek before a
/// wide launch. Everything is cached (search 30 min, stores a day, leaflets 6 h), so a busy page asks Tjek rarely.</summary>
public sealed class TjekClient(HttpClient http, IMemoryCache cache, TimeProvider clock, ILogger<TjekClient> log) : IOfferSource
{
    private const string Api = "https://squid-api.tjek.com/v2";
    private const string Unavailable = "Tilbudsavisen svarer ikke lige nu. Prøv igen om lidt.";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<List<Offer>> SearchAsync(string term, double lat, double lng, int km, CancellationToken ct)
    {
        var key = $"q|{term.ToLowerInvariant()}|{lat:F2}|{lng:F2}|{km}";
        if (cache.TryGetValue(key, out List<Offer>? hit) && hit is not null) return hit;
        var url = $"{Api}/offers/search?query={Uri.EscapeDataString(term)}&r_lat={lat.ToString(Inv)}&r_lng={lng.ToString(Inv)}&r_radius={km * 1000}&limit=100";
        try
        {
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
            var list = Parse(doc.RootElement, clock.GetUtcNow());
            cache.Set(key, list, TimeSpan.FromMinutes(30));
            return list;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            log.LogWarning(e, "Tjek search failed for {Term}", term);
            throw new SourceUnavailableException(Unavailable);
        }
    }

    // Where: the nearest store of a chain (Tjek /v2/stores), cached a day per chain and ~1 km square.
    public async Task<NearestStore?> NearestStoreAsync(string dealerId, double lat, double lng, CancellationToken ct)
    {
        var key = $"s|{dealerId}|{lat:F2}|{lng:F2}";
        if (cache.TryGetValue(key, out NearestStore? hit)) return hit;
        NearestStore? result = null;
        try
        {
            var url = $"{Api}/stores?dealer_ids={Uri.EscapeDataString(dealerId)}&r_lat={lat.ToString(Inv)}&r_lng={lng.ToString(Inv)}&r_radius=50000&order_by=distance&limit=1";
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var s = doc.RootElement[0];
                var km = Geo.Km(lat, lng, s.GetProperty("latitude").GetDouble(), s.GetProperty("longitude").GetDouble());
                result = new NearestStore($"{Str(s, "street")}, {Str(s, "zip_code")} {Str(s, "city")}".Trim(' ', ','), Math.Round(km, 1));
            }
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException or InvalidOperationException)
        {
            log.LogInformation(e, "Nearest store lookup failed for {Dealer}", dealerId);
        }
        cache.Set(key, result, TimeSpan.FromDays(1));
        return result;
    }

    // Leaflets are national, so one list serves everyone in the same ~100 km square; kept 6 hours.
    public async Task<List<Leaflet>> LeafletsAsync(double lat, double lng, CancellationToken ct)
    {
        var key = $"cat|{Math.Round(lat):F0}|{Math.Round(lng):F0}";
        if (cache.TryGetValue(key, out List<Leaflet>? hit) && hit is not null) return hit;
        try
        {
            using var doc = JsonDocument.Parse(await http.GetStringAsync($"{Api}/catalogs?r_lat={lat.ToString(Inv)}&r_lng={lng.ToString(Inv)}&r_radius=30000&limit=100", ct));
            var list = doc.RootElement.EnumerateArray().Select(c => new Leaflet(
                c.GetProperty("id").GetString()!, Str(c, "dealer", "name") ?? "", Str(c, "label") ?? "",
                c.TryGetProperty("offer_count", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 0,
                DateTimeOffset.Parse(c.GetProperty("run_from").GetString()!, Inv), DateTimeOffset.Parse(c.GetProperty("run_till").GetString()!, Inv))).ToList();
            cache.Set(key, list, TimeSpan.FromHours(6));
            return list;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            log.LogWarning(e, "Tjek leaflet list failed");
            throw new SourceUnavailableException("Tilbudsaviserne svarer ikke lige nu. Prøv igen om lidt.");
        }
    }

    public async Task<List<Offer>> LeafletOffersAsync(Leaflet leaflet, CancellationToken ct)
    {
        var key = "cat-offers|" + leaflet.Id;
        if (cache.TryGetValue(key, out List<Offer>? hit) && hit is not null) return hit;
        var offers = new List<Offer>();
        for (var offset = 0; offset < Math.Min(leaflet.OfferCount, ThisWeeksAppGap.MaxOffersPerLeaflet); offset += 100)
        {
            try
            {
                using var doc = JsonDocument.Parse(await http.GetStringAsync($"{Api}/offers?catalog_id={Uri.EscapeDataString(leaflet.Id)}&limit=100&offset={offset}", ct));
                offers.AddRange(Parse(doc.RootElement, clock.GetUtcNow()));
            }
            catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
            {
                log.LogInformation(e, "Leaflet {Id} page {Offset} failed", leaflet.Id, offset);
            }
        }
        cache.Set(key, offers, TimeSpan.FromHours(6));
        return offers;
    }

    /// <summary>Tjek's offer JSON -> grocery offers that still run, with unit price, app price and one-day deals read.</summary>
    internal static List<Offer> Parse(JsonElement root, DateTimeOffset now)
    {
        var list = new List<Offer>();
        if (root.ValueKind != JsonValueKind.Array) return list;
        foreach (var o in root.EnumerateArray())
        {
            var chain = Str(o, "dealer", "name") ?? "";
            if (!GroceryChains.Contains(chain)) continue;
            var till = Date(o, "run_till");
            if (till is { } t && t < now) continue;
            var price = Num(o, "pricing", "price");
            if (price is not > 0) continue;
            var (unitPrice, unitLabel, size) = o.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Object
                ? UnitPrice.Compute(price.Value, Num(q, "pieces", "from"), Num(q, "size", "from"), Num(q, "size", "to"),
                    Str(q, "unit", "si", "symbol"), Num(q, "unit", "si", "factor"), Str(q, "unit", "symbol"))
                : (null, null, null);
            var text = (Str(o, "heading") ?? "") + " " + (Str(o, "description") ?? "");
            var from = Date(o, "run_from");
            if (LeafletText.OnlyDay(text, from ?? now) is { } day) { from = day; till = day.AddDays(1).AddSeconds(-1); }
            if (till is { } t2 && t2 < now) continue;
            var offer = new Offer(chain, Str(o, "heading") ?? "", Str(o, "description"), price.Value, Num(o, "pricing", "pre_price"),
                unitPrice, unitLabel, size, from, till, Str(o, "images", "view") ?? Str(o, "images", "thumb"),
                Str(o, "dealer", "logo"), Condition: LeafletText.ConditionOf(text), DealerId: Str(o, "dealer_id"));
            var (app, only, appPrice, noApp) = LeafletText.AppInfo(text, chain, price.Value, unitPrice);
            list.Add(app is null ? offer : offer with { AppName = app, NeedsApp = only ? app : null, AppPrice = appPrice, NoAppPrice = noApp });
        }
        return OfferRanking.Sorted(list);
    }

    private static JsonElement? Walk(JsonElement e, string[] path)
    {
        foreach (var p in path)
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(p, out e)) return null;
        return e;
    }

    private static string? Str(JsonElement e, params string[] path) =>
        Walk(e, path) is { ValueKind: JsonValueKind.String } s ? s.GetString() : null;

    private static decimal? Num(JsonElement e, params string[] path) =>
        Walk(e, path) is { ValueKind: JsonValueKind.Number } n && n.TryGetDecimal(out var d) ? d : null;

    private static DateTimeOffset? Date(JsonElement e, params string[] path) =>
        Str(e, path) is { } s && DateTimeOffset.TryParse(s, Inv, DateTimeStyles.None, out var d) ? d : null;
}
