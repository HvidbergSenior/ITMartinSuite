using System.Collections.Concurrent;

namespace ITMartinLager.Server.Services;

// One listing as found - the proof behind a price (user 2026-10-07: "it needs proof").
public sealed record Offer(string Name, decimal? Price, string Url);

// Low-High = the realistic asking prices; Wild = the highest "urimelig internetpris" (over 3x the middle), if any.
// Proof = up to 3 listings nearest the middle price. Ambiguous = the title is too broad to know they are this book
// ("Zombie", "Flint" without an author) - then there are no prices, so nothing can be priced from it.
public sealed record Listings(int Count, decimal? Low, decimal? Median, decimal? High, string Url, List<Offer> Proof,
    decimal? Wild = null, bool Ambiguous = false, decimal? NewPrice = null)
{
    public bool HasPrices => Low is not null;

    public string Text => Count == 0 ? "ingen til salg"
        : Ambiguous ? $"{Count} fund, men titlen er for bred til at vide om det er denne bog – ikke bevis"
        : Count == 0 && NewPrice is { } np0 ? $"ingen brugte, ny i butikken {np0:0} kr"
        : Low is null ? $"{Count} til salg"
        : $"{Count} til salg, {Low:0}-{High:0} kr (midt {Median:0} kr)" + (Wild is { } w ? $", urimelige internetpriser op til {w:0} kr" : "")
            + (NewPrice is { } np ? $", ny i butikken {np:0} kr" : "");
}

// What the same book is offered for on DBA right now (user 2026-10-07: "price and efterspørgsel"). There is no free
// Danish used-book price API: antikvariat.net is behind Cloudflare and Bogbasen searches in the browser, but DBA's
// search page carries the listings as schema.org JSON-LD (name + asking price). Asking prices, not sold prices - so it
// is a supply count and a price range, never the selling price by itself. Cached a day, two requests at a time.
public sealed class DbaListings(HttpClient http, ILogger<DbaListings> log)
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
            var result = ListingParse.Parse(await res.Content.ReadAsStringAsync(ct), title, author, SearchUrl(query), _ => 1m);
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
}
