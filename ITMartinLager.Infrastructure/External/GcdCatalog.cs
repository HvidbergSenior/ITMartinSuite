using System.Collections.Concurrent;
using System.Text.Json;

namespace ITMartinLager.Infrastructure.External;

// Grand Comics Database (comics.org) - free, run by volunteers, with an open REST API (2026-10-25). Asked gently: one
// request at a time, a series name is looked up once a week (the answers are kept), and it says who we are.
public sealed class GcdCatalog(HttpClient http, ILogger<GcdCatalog> log) : IComicCatalog
{
    private const string Ua = "BogshoppenLager/1.0 (ITMartin@Mensa.dk)";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, (DateTime At, List<CatalogSeries> Series)> SeriesCache = new();
    private static readonly ConcurrentDictionary<string, int?> IssueYears = new();

    public async Task<int?> YearAsync(string series, string number, CancellationToken ct)
    {
        var all = await SeriesAsync(series.Trim(), ct);
        if (all is null || ComicCatalog.Pick(all, series, number) is not { } hit) return null;
        if (ComicCatalog.YearWithoutIssue(hit.Series, hit.Index) is { } y) return y;
        var url = hit.Series.IssueUrls.ElementAtOrDefault(hit.Index);
        return url is null ? null : await IssueYearAsync(url, ct);
    }

    private async Task<List<CatalogSeries>?> SeriesAsync(string name, CancellationToken ct)
    {
        var key = name.ToLowerInvariant();
        if (SeriesCache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-7)) return hit.Series;
        var list = new List<CatalogSeries>();
        var url = $"https://www.comics.org/api/series/name/{Uri.EscapeDataString(name)}/?format=json";
        // At most 3 pages (300 series) - a name that common is not one answer anyway.
        for (var page = 0; page < 3 && url is not null; page++)
        {
            using var doc = await GetAsync(url, ct);
            if (doc is null) return null;
            foreach (var s in doc.RootElement.GetProperty("results").EnumerateArray())
                list.Add(new CatalogSeries(Str(s, "name"), Str(s, "country"), Int(s, "year_began"), Int(s, "year_ended"),
                    Strings(s, "issue_descriptors"), Strings(s, "active_issues")));
            url = doc.RootElement.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String ? next.GetString() : null;
        }
        SeriesCache[key] = (DateTime.UtcNow, list);
        return list;
    }

    private async Task<int?> IssueYearAsync(string url, CancellationToken ct)
    {
        if (IssueYears.TryGetValue(url, out var known)) return known;
        using var doc = await GetAsync(url.Contains("format=") ? url : url + (url.Contains('?') ? "&" : "?") + "format=json", ct);
        if (doc is null) return null;
        var year = ComicCatalog.YearOfKeyDate(Str(doc.RootElement, "key_date"));
        IssueYears[url] = year;
        return year;
    }

    private async Task<JsonDocument?> GetAsync(string url, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(Ua);
            req.Headers.Accept.ParseAdd("application/json");
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) { log.LogInformation("GCD {Status} for {Url}", res.StatusCode, url); return null; }
            return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("GCD failed for {Url}: {Message}", url, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static List<string> Strings(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : "").ToList() : [];
}
