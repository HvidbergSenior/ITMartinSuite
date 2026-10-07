using System.Collections.Concurrent;

namespace ITMartinLager.Server.Services;

// The international market (user 2026-10-07: Gotrek & Felix Third Omnibus was valued far too low - DBA hardly has
// English collector books, AbeBooks lists it at $60-150). AbeBooks' search page carries the listings as JSON-LD like
// DBA; prices are in USD, turned into kr with the ECB rate from frankfurter.app (free, no key, cached a day).
// Asking prices, not sold prices.
public sealed class AbeBooksListings(HttpClient http, ILogger<AbeBooksListings> log)
{
    private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128 Safari/537.36";
    private static readonly SemaphoreSlim Gate = new(2, 2);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    public static string SearchUrl(string query) =>
        "https://www.abebooks.com/servlet/SearchResults?kn=" + Uri.EscapeDataString(query);

    public async Task<Listings?> LookupAsync(string title, string author, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var query = DbaListings.QueryFor(title, author);
        var key = query.ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        await Gate.WaitAsync(ct);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, SearchUrl(query));
            req.Headers.UserAgent.ParseAdd(Ua);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) { log.LogInformation("AbeBooks {Status} for {Query}", res.StatusCode, query); return null; }
            var html = await res.Content.ReadAsStringAsync(ct);
            var usd = await FxRates.ToDkkAsync(http, "USD", log, ct);
            var result = ListingParse.Parse(html, title, author, SearchUrl(query), cur => cur == "USD" ? usd : null);
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("AbeBooks lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

}
