using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ITMartinLager.Server.Services;

// Listings from a search page's schema.org JSON-LD - DBA (CollectionPage.mainEntity.itemListElement) and AbeBooks
// (ItemList.itemListElement) carry the same shape: item.name + item.offers.price/priceCurrency.
public static partial class ListingParse
{
    private static readonly HashSet<string> Stop = ["the", "and", "der", "den", "det", "som", "for", "med", "til", "von", "les", "una"];

    // Only listings whose name holds the title's words count - sites also return the author's other books.
    // toDkk: currency -> rate to kr, null = unknown currency (the price is skipped, the listing still counts).
    public static Listings Parse(string html, string title, string url, Func<string, decimal?> toDkk)
    {
        var words = Words(title);
        var prices = new List<decimal>();
        var examples = new List<string>();
        var count = 0;
        foreach (Match m in LdJson().Matches(html))
        {
            JsonElement root;
            try { root = JsonDocument.Parse(m.Groups[1].Value).RootElement; } catch (JsonException) { continue; }
            if (root.ValueKind != JsonValueKind.Object) continue;
            var holder = root.TryGetProperty("mainEntity", out var main) ? main : root;
            if (holder.ValueKind != JsonValueKind.Object || !holder.TryGetProperty("itemListElement", out var list)
                || list.ValueKind != JsonValueKind.Array) continue;
            foreach (var el in list.EnumerateArray())
            {
                // Breadcrumb lists use the same shape with a URL string as item - only real objects are listings.
                if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) continue;
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (!Matches(name, words)) continue;
                count++;
                if (item.TryGetProperty("offers", out var offers) && offers.ValueKind == JsonValueKind.Object && offers.TryGetProperty("price", out var p)
                    && decimal.TryParse(p.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price > 0)
                {
                    var cur = offers.TryGetProperty("priceCurrency", out var c) ? c.GetString() ?? "DKK" : "DKK";
                    if ((cur == "DKK" ? 1m : toDkk(cur)) is { } rate) prices.Add(Math.Round(price * rate));
                }
                if (examples.Count < 3) examples.Add(name);
            }
        }
        return Summarise(count, prices, url, examples);
    }

    // Asking prices over 3x the middle one are kept apart as "urimelige internetpriser" (user 2026-10-07: show them,
    // but they are not the market) - so one $1000 dreamer does not turn a 400 kr book into a 900 kr book.
    internal static Listings Summarise(int count, List<decimal> prices, string url, List<string> examples)
    {
        prices.Sort();
        if (prices.Count == 0) return new Listings(count, null, null, null, url, examples);
        var median = prices[prices.Count / 2];
        var sane = prices.Where(p => p <= median * 3).ToList();
        decimal? wild = prices[^1] > median * 3 ? prices[^1] : null;
        return new Listings(count, sane[0], median, sane[^1], url, examples, wild);
    }

    internal static List<string> Words(string s) =>
        WordRx().Matches(s.ToLowerInvariant()).Select(m => m.Value).Where(w => w.Length >= 3 && !Stop.Contains(w)).Distinct().ToList();

    // All significant title words for a short title, at least 2/3 of them for a long one ("Amager digte" = "Amagerdigte").
    internal static bool Matches(string name, List<string> words)
    {
        if (words.Count == 0) return false;
        var squashed = string.Concat(Words(name));
        var found = words.Count(w => squashed.Contains(w));
        return words.Count <= 2 ? found == words.Count : found * 3 >= words.Count * 2;
    }

    [GeneratedRegex("""<script type="application/ld\+json"[^>]*>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex LdJson();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRx();
}
