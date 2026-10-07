using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ITMartinLager.Server.Services;

public sealed record Listings(int Count, decimal? Low, decimal? Median, decimal? High, string Url, List<string> Examples);

// What the same book is offered for on DBA right now (user 2026-10-07: "price and efterspørgsel"). There is no free
// Danish used-book price API: antikvariat.net is behind Cloudflare and Bogbasen searches in the browser, but DBA's
// search page carries the listings as schema.org JSON-LD (name + asking price). Asking prices, not sold prices - so it
// is a supply count and a price range, never the selling price by itself. Cached a day, two requests at a time.
public sealed partial class DbaListings(HttpClient http, ILogger<DbaListings> log)
{
    private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128 Safari/537.36";
    private static readonly SemaphoreSlim Gate = new(2, 2);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public static string SearchUrl(string query) =>
        "https://www.dba.dk/recommerce/forsale/search?q=" + Uri.EscapeDataString(query);

    // Author's surname + title is what sellers write ("Amagerdigte, Klaus Rifbjerg").
    public static string QueryFor(string title, string author)
    {
        var surname = author.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        return $"{surname} {title}".Trim();
    }

    public async Task<Listings?> LookupAsync(string title, string author, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var query = QueryFor(title, author);
        var key = query.ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        await Gate.WaitAsync(ct);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, SearchUrl(query));
            req.Headers.UserAgent.ParseAdd(Ua);
            req.Headers.AcceptLanguage.ParseAdd("da-DK,da;q=0.9");
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) { log.LogInformation("DBA {Status} for {Query}", res.StatusCode, query); return null; }
            var result = Parse(await res.Content.ReadAsStringAsync(ct), title, SearchUrl(query));
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("DBA lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    // Only listings whose name holds the title's words count - DBA also returns the author's other books.
    internal static Listings Parse(string html, string title, string url)
    {
        var words = Words(title);
        var prices = new List<decimal>();
        var examples = new List<string>();
        var count = 0;
        foreach (Match m in LdJson().Matches(html))
        {
            JsonElement root;
            try { root = JsonDocument.Parse(m.Groups[1].Value).RootElement; } catch (JsonException) { continue; }
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("mainEntity", out var main)
                || !main.TryGetProperty("itemListElement", out var list) || list.ValueKind != JsonValueKind.Array) continue;
            foreach (var el in list.EnumerateArray())
            {
                if (!el.TryGetProperty("item", out var item)) continue;
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (!Matches(name, words)) continue;
                count++;
                if (item.TryGetProperty("offers", out var offers) && offers.TryGetProperty("price", out var p)
                    && decimal.TryParse(p.ToString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var price)
                    && price > 0)
                    prices.Add(price);
                if (examples.Count < 3) examples.Add(name);
            }
        }
        prices.Sort();
        return new Listings(count,
            prices.Count > 0 ? prices[0] : null, prices.Count > 0 ? prices[prices.Count / 2] : null,
            prices.Count > 0 ? prices[^1] : null, url, examples);
    }

    internal static List<string> Words(string s) =>
        WordRx().Matches(s.ToLowerInvariant()).Select(m => m.Value).Where(w => w.Length >= 3).Distinct().ToList();

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
