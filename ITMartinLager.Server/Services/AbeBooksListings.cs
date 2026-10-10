using System.Collections.Concurrent;

namespace ITMartinLager.Server.Services;

// AbeBooks and its German sister ZVAB - the international antiquarian market. Their search result pages carry the
// listings as JSON-LD like DBA. The /book-search/ (/buch-suchen/) addresses are used because robots.txt allows them
// (the /servlet/ search is disallowed, 2026-10-10). By ISBN when there is one - then every hit is that book.
// Prices in USD/EUR, turned into kr with the ECB rate (frankfurter.dev, cached a day). Asking prices, not sold prices.
public abstract class AbeFamilyListings(HttpClient http, ILogger log, string host, string path, string currency, string name)
{
    private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128 Safari/537.36";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    protected static string Slug(string s) => Uri.EscapeDataString(string.Join('-', ListingParse.Words(s)));

    // Author's surname only - "Pratchett, Terry", "Terry Pratchett" and "T. Pratchett" are all listed.
    private string TitleUrl(string title, string author)
    {
        var surname = ListingParse.Words(author).LastOrDefault() ?? "";
        return $"https://{host}/{path}/title/{Slug(title)}/" + (surname != "" ? $"author/{Uri.EscapeDataString(surname)}/" : "");
    }

    public async Task<Listings?> LookupAsync(string title, string author, string isbn = "", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title) && isbn == "") return null;
        var url = isbn != "" ? $"https://{host}/{path}/isbn/{isbn}/" : TitleUrl(title, author);
        if (Cache.TryGetValue(url, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        var gate = Gates.GetOrAdd(host, _ => new SemaphoreSlim(2, 2));
        await gate.WaitAsync(ct);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(Ua);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) { log.LogInformation("{Name} {Status} for {Url}", name, res.StatusCode, url); return null; }
            var html = await res.Content.ReadAsStringAsync(ct);
            var rate = await FxRates.ToDkkAsync(http, currency, log, ct);
            // By ISBN every listing is the book, so only the author's name has to be there (title "" = no title check).
            var result = isbn != ""
                ? ListingParse.Parse(html, "", "", url, cur => cur == currency ? rate : null, anyName: true)
                : ListingParse.Parse(html, title, author, url, cur => cur == currency ? rate : null);
            Cache[url] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("{Name} lookup failed for {Url}: {Message}", name, url, ex.Message);
            return null;
        }
        finally { gate.Release(); }
    }
}

// The international market (user 2026-10-07: Gotrek & Felix Third Omnibus was valued far too low - DBA hardly has
// English collector books, AbeBooks lists it at $60-150).
public sealed class AbeBooksListings(HttpClient http, ILogger<AbeBooksListings> log)
    : AbeFamilyListings(http, log, "www.abebooks.com", "book-search", "USD", "AbeBooks")
{
    public static string SearchUrl(string query) => $"https://www.abebooks.com/book-search/kw/{Slug(query)}/";
}

// ZVAB - the German antiquarian booksellers (2026-10-10, user: "as many places as possible"). Prices in EUR.
public sealed class ZvabListings(HttpClient http, ILogger<ZvabListings> log)
    : AbeFamilyListings(http, log, "www.zvab.com", "buch-suchen", "EUR", "ZVAB")
{
    public static string SearchUrl(string query) => $"https://www.zvab.com/buch-suchen/kw/{Slug(query)}/";
}
