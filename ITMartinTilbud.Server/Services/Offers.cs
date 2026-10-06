using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace ITMartinTilbud.Server.Services;

// One offer as the page shows it. UnitPrice is per kg, per litre or per piece, so a 3-pack and a single bag compare.
public sealed record Offer(
    string Chain, string Title, string? Description, decimal Price, decimal? NormalPrice,
    decimal? UnitPrice, string? UnitLabel, string? Size, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo,
    string? Image, string? Logo, bool Variant = false);

// Weekly offers from Tjek (the company behind eTilbudsavis): one search covers Lidl, Netto, REMA 1000, føtex, Bilka,
// Kvickly, SuperBrugsen, MENY, Løvbjerg, SPAR, 365discount and more. No key. Unofficial API - ask Tjek before a wide launch.
// Answers are cached 30 min per search + place, so a busy page asks Tjek rarely.
public sealed class TjekOffers(HttpClient http, IMemoryCache cache, ILogger<TjekOffers> log)
{
    public const int MaxKm = 50;

    public async Task<List<Offer>> SearchAsync(string query, double lat, double lng, int km, CancellationToken ct)
    {
        query = query.Trim();
        km = Math.Clamp(km, 1, MaxKm);
        var key = $"q|{query.ToLowerInvariant()}|{lat:F2}|{lng:F2}|{km}";
        if (cache.TryGetValue(key, out List<Offer>? hit) && hit is not null) return hit;

        var url = "https://squid-api.tjek.com/v2/offers/search?query=" + Uri.EscapeDataString(query) +
                  $"&r_lat={lat.ToString(CultureInfo.InvariantCulture)}&r_lng={lng.ToString(CultureInfo.InvariantCulture)}" +
                  $"&r_radius={km * 1000}&limit=100";
        List<Offer> list;
        try
        {
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
            list = Relevant(Parse(doc.RootElement, DateTimeOffset.UtcNow), query);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            log.LogWarning(e, "Tjek search failed for {Query}", query);
            throw new OffersUnavailableException();
        }
        cache.Set(key, list, TimeSpan.FromMinutes(30));
        return list;
    }

    // Groceries only: Tjek also lists building centres, electronics and travel agents.
    internal static readonly HashSet<string> GroceryChains = new(StringComparer.OrdinalIgnoreCase)
    {
        "Lidl", "Netto", "REMA 1000", "føtex", "Bilka", "Kvickly", "SuperBrugsen", "Brugsen", "Dagli'Brugsen", "MENY",
        "Løvbjerg", "SPAR", "Min Købmand", "LET-KØB", "365discount", "Coop 365", "ABC Lavpris", "Fakta", "Irma",
        "Nærkøb", "Salling", "Wolt Market", "Coop.dk MAD", "Lokal Bager", "Normal", "Basic & More", "Bilka ToGo",
    };

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
            var (unitPrice, unitLabel, size) = Unit(o, price.Value);
            list.Add(new Offer(chain, Str(o, "heading") ?? "", Str(o, "description"), price.Value, Num(o, "pricing", "pre_price"),
                unitPrice, unitLabel, size, Date(o, "run_from"), till, Str(o, "images", "view") ?? Str(o, "images", "thumb"),
                Str(o, "dealer", "logo")));
        }
        // Only like with like: the most common unit first (kr/kg for coffee, kr/l for milk), cheapest first within it;
        // then the other units, and offers without a size last. Capsules at 3 kr/stk must not beat coffee at 60 kr/kg.
        var rank = list.Where(x => x.UnitLabel is not null).GroupBy(x => x.UnitLabel!)
            .OrderByDescending(g => g.Count()).Select((g, i) => (g.Key, i)).ToDictionary(t => t.Key, t => t.i);
        return list.OrderBy(x => x.UnitLabel is { } u ? rank[u] : int.MaxValue).ThenBy(x => x.UnitPrice ?? x.Price).ToList();
    }

    // Tjek's search is loose ("smør" also brought potatoes): keep offers whose title or text has every searched word;
    // with 3+ title matches only those (margarine that says "smør" in its text is not butter). Order (per kg) is kept.
    // If nothing has the words, show what Tjek found rather than nothing.
    // Then (user 2026-10-06: "mælk" gave kakaomælk and shower gel): non-food goes, unless that is what was searched for,
    // and flavoured kinds (kakaomælk, jordbæryoghurt) are marked Variant and go last - the page folds them away.
    internal static List<Offer> Relevant(List<Offer> list, string query)
    {
        var q = query.ToLowerInvariant();
        var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool Has(string? s) => s is not null && words.All(w => s.Contains(w, StringComparison.OrdinalIgnoreCase));
        var food = NonFood.Any(n => q.Contains(n)) ? list : list.Where(o => !NonFood.Any(n => o.Title.Contains(n, StringComparison.OrdinalIgnoreCase))).ToList();
        var inTitle = food.Where(o => Has(o.Title)).ToList();
        var hits = inTitle.Count >= 3 ? inTitle : food.Where(o => Has(o.Title + " " + o.Description)).ToList();
        if (hits.Count == 0) return food.Count > 0 ? food : list;
        var marked = hits.Select(o => IsVariant(o.Title, words) ? o with { Variant = true } : o).ToList();
        return marked.Where(o => !o.Variant).Concat(marked.Where(o => o.Variant)).ToList();
    }

    internal static readonly string[] NonFood =
    [
        "shower", "showergel", "shampoo", "balsam", "sæbe", "bodylotion", "lotion", "creme", "deodorant", "deo ", "tandpasta",
        "vaskemiddel", "opvask", "skyllemiddel", "rengøring", "bleer", "vatrondel", "barbér", "hårfarve", "solcreme", "kattemad", "hundemad",
    ];

    // A flavour glued onto the searched word ("kakaomælk", "kakao-skummetmælk", "jordbæryoghurt") is another product than
    // the word itself. Kinds of the thing itself (skummetmælk, minimælk, letmælk) are not flavours and stay main.
    internal static readonly string[] Flavours =
        ["kakao", "choko", "chokolade", "jordbær", "vanilje", "banan", "hindbær", "kaffe", "karamel", "mokka", "milkshake", "is "];

    internal static bool IsVariant(string title, string[] words)
    {
        var t = title.ToLowerInvariant();
        if (words.Any(w => Flavours.Any(f => w.Contains(f.Trim())))) return false;   // searched for the flavour itself
        // Every place a searched word occurs: is it glued to a flavour just before it?
        foreach (var w in words)
        {
            var hitPlain = false;
            for (var i = t.IndexOf(w, StringComparison.Ordinal); i >= 0; i = t.IndexOf(w, i + 1, StringComparison.Ordinal))
            {
                var start = i;
                while (start > 0 && (char.IsLetter(t[start - 1]) || t[start - 1] == '-')) start--;
                var prefix = t[start..i];
                if (!Flavours.Any(f => prefix.Contains(f.Trim()))) hitPlain = true;
            }
            if (hitPlain) return false;
        }
        return true;
    }

    // "quantity": { unit: { si: { symbol: kg|l, factor } }, size: { from, to }, pieces: { from, to } }
    internal static (decimal? price, string? label, string? size) Unit(JsonElement o, decimal price)
    {
        if (!o.TryGetProperty("quantity", out var q) || q.ValueKind != JsonValueKind.Object) return (null, null, null);
        var pieces = Num(q, "pieces", "from") is { } p && p > 0 ? p : 1m;
        var from = Num(q, "size", "from");
        var to = Num(q, "size", "to");
        var symbol = Str(q, "unit", "si", "symbol");
        var factor = Num(q, "unit", "si", "factor") ?? 1m;
        var unit = Str(q, "unit", "symbol") is "pcs" ? "stk" : Str(q, "unit", "symbol");
        string? size = from is > 0 ? $"{(pieces > 1 ? $"{pieces:0} × " : "")}{from.Value:0.###}{(to is { } t && t != from ? $"–{t:0.###}" : "")} {unit}" : null;

        if (from is > 0 && symbol is "kg" or "l")
        {
            // A range ("400-500 g") is priced on the larger size: the honest lower bound of what you pay per kg.
            var amount = (to is > 0 ? Math.Max(from.Value, to.Value) : from.Value) * factor * pieces;
            if (amount > 0) return (Math.Round(price / amount, 2), symbol == "kg" ? "kr/kg" : "kr/l", size);
        }
        if (unit is "pcs" or "stk" && from is > 0)
            return (Math.Round(price / (from.Value * pieces), 2), "kr/stk", size);
        return (null, null, size);
    }

    private static JsonElement? Walk(JsonElement e, string[] path)
    {
        foreach (var p in path)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(p, out e)) return null;
        }
        return e;
    }

    private static string? Str(JsonElement e, params string[] path) =>
        Walk(e, path) is { ValueKind: JsonValueKind.String } s ? s.GetString() : null;

    private static decimal? Num(JsonElement e, params string[] path) =>
        Walk(e, path) is { ValueKind: JsonValueKind.Number } n && n.TryGetDecimal(out var d) ? d : null;

    private static DateTimeOffset? Date(JsonElement e, params string[] path) =>
        Str(e, path) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

public sealed class OffersUnavailableException : Exception;

// Postcode -> place, from OpenStreetMap's Nominatim (DAWA closed in 2026). Cached for a day; Nominatim asks for 1 request/second.
public sealed class Places(HttpClient http, IMemoryCache cache)
{
    public sealed record Place(string Name, double Lat, double Lng);

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
