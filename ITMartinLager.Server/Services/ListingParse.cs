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
    public static Listings Parse(string html, string title, string author, string url, Func<string, decimal?> toDkk)
    {
        var match = Matcher(title, author);
        var offers = new List<Offer>();
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
                if (!match.Matches(name)) continue;
                var link = item.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? url : url;
                decimal? kr = null;
                if (item.TryGetProperty("offers", out var o) && o.ValueKind == JsonValueKind.Object)
                {
                    if (o.TryGetProperty("url", out var ou) && ou.ValueKind == JsonValueKind.String) link = ou.GetString() ?? link;
                    if (o.TryGetProperty("price", out var p)
                        && decimal.TryParse(p.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price > 0)
                    {
                        var cur = o.TryGetProperty("priceCurrency", out var c) ? c.GetString() ?? "DKK" : "DKK";
                        if ((cur == "DKK" ? 1m : toDkk(cur)) is { } rate) kr = Math.Round(price * rate);
                    }
                }
                offers.Add(new Offer(name, kr, link));
            }
        }
        return Summarise(offers, url, match.Ambiguous);
    }

    // Asking prices over 3x the middle one are kept apart as "urimelige internetpriser" (user 2026-10-07: show them,
    // but they are not the market) - so one $1000 dreamer does not turn a 400 kr book into a 900 kr book.
    // Ambiguous: the listings are counted, but no price comes out of them - they may be other books.
    internal static Listings Summarise(List<Offer> offers, string url, bool ambiguous = false)
    {
        var priced = offers.Where(o => o.Price is not null).OrderBy(o => o.Price).ToList();
        if (ambiguous || priced.Count == 0) return new Listings(offers.Count, null, null, null, url, [], null, ambiguous && offers.Count > 0);
        var median = priced[priced.Count / 2].Price!.Value;
        var sane = priced.Where(o => o.Price <= median * 3).ToList();
        decimal? wild = priced[^1].Price > median * 3 ? priced[^1].Price : null;
        var proof = sane.OrderBy(o => Math.Abs(o.Price!.Value - median)).Take(3).OrderBy(o => o.Price).ToList();
        return new Listings(offers.Count, sane[0].Price, median, sane[^1].Price, url, proof, wild);
    }

    internal static List<string> Words(string s) =>
        WordRx().Matches(s.ToLowerInvariant()).Select(m => m.Value).Where(w => w.Length >= 3 && !Stop.Contains(w)).Distinct().ToList();

    public sealed record TitleMatch(List<string> Words, string Surname, bool Ambiguous)
    {
        // Every significant title word must be there ("Amager digte" = "Amagerdigte") - "First Omnibus" is not proof for
        // "Third Omnibus". Only a title of 5+ words may miss one (subtitles are written differently). A one-word title
        // must also have the author's surname. Films, games and merchandise of the same name are never proof for a book.
        public bool Matches(string name)
        {
            if (Words.Count == 0) return false;
            var listing = ListingParse.Words(name);
            if (listing.Any(NotABook.Contains)) return false;
            var squashed = string.Concat(listing);
            var found = Words.Count(w => squashed.Contains(w));
            var ok = found >= (Words.Count >= 5 ? Words.Count - 1 : Words.Count);
            return ok && (Words.Count > 1 || Surname == "" || squashed.Contains(Surname));
        }
    }

    private static readonly HashSet<string> NotABook =
        ["vhs", "dvd", "bluray", "blu", "plakat", "poster", "puslespil", "figur", "figurer", "shirt", "krus", "kop", "film", "videobånd"];

    // "Zombie" or "Flint" alone finds everything called that - without an author it cannot prove a price.
    // A long one-word title ("Papegøjemysteriet") is specific enough on its own.
    public static TitleMatch Matcher(string title, string author)
    {
        var words = Words(title);
        var surname = Words(author).LastOrDefault() ?? "";
        return new TitleMatch(words, surname, words.Count <= 1 && surname == "" && (words.FirstOrDefault()?.Length ?? 0) < 10);
    }

    [GeneratedRegex("""<script type="application/ld\+json"[^>]*>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex LdJson();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRx();
}
