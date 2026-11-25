using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace ITMartinLager.Infrastructure.PriceSites;

// Faraos Cigarer - Denmark's big comic shop with an "antikvarisk" department (user 2026-10-07: "yes do faraos").
// The webshop is an Angular app on its own JSON API (shopws.faraos.dk, the same call its search box makes). Used
// comics carry one price per condition ("Fine -" 22 kr, "Fine" 25 kr ...) - those are the proof. New copies in the
// shop are only "nypris" (a ceiling for a used copy), and merchandise/films with the same name are skipped.
public sealed partial class FaraosListings(HttpClient http, ILogger<FaraosListings> log)
{
    private const string Api = "https://shopws.faraos.dk/api/1/items/filtered";
    private static readonly SemaphoreSlim Gate = new(2, 2);
    private static readonly ConcurrentDictionary<string, (DateTime At, Listings Result)> Cache = new();

    // The shop has no search address (search is a dialog), so the check link opens the used department.
    public static string SearchUrl(string query) => "https://www.faraos.dk/antikvarisk";

    public async Task<Listings?> LookupAsync(string title, string author, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var query = title.Trim();
        var key = (query + "|" + author).ToLowerInvariant();
        if (Cache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddDays(-1)) return hit.Result;

        await Gate.WaitAsync(ct);
        try
        {
            // An Anders And & Co. issue has its own page with a price per condition even when no copy is in stock, and the
            // search only lists copies in stock (2026-11-25: "1969 nr. 19" was on the site but the app found nothing).
            if (IssueQuery(query) is { } issue && await IssueJsonAsync(issue.Year, issue.Number, ct) is { } direct
                && Parse(direct, title, author, SearchUrl(query)) is { Count: > 0 } exact)
            {
                Cache[key] = (DateTime.UtcNow, exact);
                return exact;
            }
            // Their search answers 500 on some "Nr. 3" searches (2026-10-07: "Tintin Nr 3" fails, "Tintin 3" works),
            // so "Nr." is left out; if it still fails, once more with the words only.
            var json = await SearchAsync(NrRx().Replace(query, " "), ct)
                       ?? await SearchAsync(string.Join(' ', ListingParse.Words(query).Where(w => !ListingParse.IsNumber(w))), ct);
            if (json is null) { log.LogInformation("Faraos search failed for {Query}", query); return null; }
            var result = Parse(json, title, author, SearchUrl(query));
            // A broad search ("Basserne") fills page 1 with new books; the used copies start on page 2 (2026-10-26).
            if (result.All.Count == 0 && result.Broad.Count == 0
                && await SearchAsync(NrRx().Replace(query, " "), ct, page: 2) is { } used
                && Parse(used, title, author, SearchUrl(query)) is { Count: > 0 } second)
                result = second with { NewPrice = result.NewPrice };
            Cache[key] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("Faraos lookup failed for {Query}: {Message}", query, ex.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    private async Task<string?> SearchAsync(string text, CancellationToken ct, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var body = JsonSerializer.Serialize(new { inSearchDialog = true, searchString = text.Trim(), tag = (string?)null, sortOrder = 5, subPage = 0, page });
        using var req = new HttpRequestMessage(HttpMethod.Post, Api) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        req.Headers.TryAddWithoutValidation("Origin", "https://www.faraos.dk");
        req.Headers.UserAgent.ParseAdd("BogshoppenLager/1.0 (ITMartin@Mensa.dk)");
        using var res = await http.SendAsync(req, ct);
        return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync(ct) : null;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\bnr\.?\s*", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex NrRx();

    // "Anders And & Co. 19 1969" (the hunter's own query: number, then year) or "Anders And & Co. 1969 Nr. 19".
    [System.Text.RegularExpressions.GeneratedRegex(
        @"^\s*(?:Walt Disney'?s\s+)?Anders And\s*(?:&|og|and)?\s*Co\.?\s+(?:(?<nr>\d{1,3})\s+(?<yr>(?:19|20)\d\d)|(?<yr2>(?:19|20)\d\d)\s+(?:nr\.?\s*)?(?<nr2>\d{1,3}))\s*$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex IssueRx();

    internal static (int Year, int Number)? IssueQuery(string query)
    {
        var m = IssueRx().Match(query);
        if (!m.Success) return null;
        var yr = m.Groups["yr"].Success ? m.Groups["yr"].Value : m.Groups["yr2"].Value;
        var nr = m.Groups["nr"].Success ? m.Groups["nr"].Value : m.Groups["nr2"].Value;
        return (int.Parse(yr), int.Parse(nr));
    }

    // The menu node "Anders And & Co" in the shop's own menu; one node per year below it, and one leaf below that.
    private const int AndersAndCoMenu = 130430;
    private static readonly ConcurrentDictionary<int, (int Id, string Path)> YearMenu = new();

    private async Task<string?> GetAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Origin", "https://www.faraos.dk");
        req.Headers.UserAgent.ParseAdd("BogshoppenLager/1.0 (ITMartin@Mensa.dk)");
        using var res = await http.SendAsync(req, ct);
        return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync(ct) : null;
    }

    /// <summary>The issue's page as the same {"items":[...]} shape the search gives, or null when the shop has no such page.</summary>
    private async Task<string?> IssueJsonAsync(int year, int number, CancellationToken ct)
    {
        if (!YearMenu.TryGetValue(year, out var leaf))
        {
            var years = await GetAsync($"https://shopws.faraos.dk/api/1/menu/items/{AndersAndCoMenu}/children/3", ct);
            if (years is null) return null;
            int? yearId = null;
            foreach (var n in JsonDocument.Parse(years).RootElement.EnumerateArray())
                if (Str(n, "name") == $"andersandco{year}" && n.TryGetProperty("id", out var id)) yearId = id.GetInt32();
            if (yearId is null) return null;
            var leaves = await GetAsync($"https://shopws.faraos.dk/api/1/menu/items/{yearId}/children/4", ct);
            if (leaves is null) return null;
            foreach (var n in JsonDocument.Parse(leaves).RootElement.EnumerateArray())
                if (n.TryGetProperty("id", out var id)) { leaf = (id.GetInt32(), Str(n, "path")); break; }
            if (leaf.Id == 0) return null;
            YearMenu[year] = leaf;
        }
        var item = await GetAsync($"https://shopws.faraos.dk/api/1/items/byitempath/{leaf.Id}/anders-and-co.-{year}-nr.-{number}/25", ct);
        if (item is null) return null;
        var node = System.Text.Json.Nodes.JsonNode.Parse(item);
        if (node is not System.Text.Json.Nodes.JsonObject obj) return null;
        obj["menuPath"] = leaf.Path;
        return new System.Text.Json.Nodes.JsonObject { ["items"] = new System.Text.Json.Nodes.JsonArray(obj) }.ToJsonString();
    }

    internal static Listings Parse(string json, string title, string author, string url)
    {
        // Comics carry no author in the name, so the author is not asked for - unless the title is one word: "Mort" by
        // Pratchett is not "Mort Walker" (2026-10-10).
        var match = ListingParse.Matcher(title, author) is var m && m.Words.Count > 1 ? m with { Surname = "" } : ListingParse.Matcher(title, author);
        var used = new List<Offer>();
        decimal? newPrice = null;
        JsonElement root;
        try { root = JsonDocument.Parse(json).RootElement; } catch (JsonException) { return ListingParse.Summarise(used, url); }
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return ListingParse.Summarise(used, url);

        foreach (var it in items.EnumerateArray())
        {
            var path = Str(it, "menuPath");
            if (path.StartsWith("/merchandise") || path.Contains("/film")) continue;
            var name = (Str(it, "displayName") + " " + Str(it, "displayName2")).Trim();
            if (!match.Matches(name)) continue;
            var link = "https://www.faraos.dk" + path + "/" + Str(it, "itemPath");

            if (it.TryGetProperty("usedComicEditions", out var eds) && eds.ValueKind == JsonValueKind.Array && eds.GetArrayLength() > 0)
            {
                foreach (var ed in eds.EnumerateArray())
                {
                    if (!ed.TryGetProperty("conditions", out var conds) || conds.ValueKind != JsonValueKind.Array) continue;
                    foreach (var c in conds.EnumerateArray())
                        if (Price(c, "price") is { } kr)
                            // Edition and grade are the shop's own fields, shown beside the price - not in the name, where
                            // "1. udgave, 1. oplag" (every Danish blad) and "Near Mint" read as a seller's collector copy and
                            // threw out nearly every Faraos copy (found 2026-10-26, Basserne 612).
                            used.Add(new Offer(name, kr, link, $"{Str(c, "name")}, {Str(ed, "editionName")}"));
                }
            }
            else if (it.TryGetProperty("status", out var st) && !(st.TryGetProperty("isUsed", out var u) && u.GetBoolean())
                     && Price(st, "normalPrice") is { } np)
                newPrice = newPrice is null ? np : Math.Min(newPrice.Value, np);
        }
        var result = ListingParse.Summarise(used, url, match.Ambiguous);
        return result with { NewPrice = newPrice };
    }

    private static string Str(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static decimal? Price(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty("price", out var v)
        && v.TryGetDecimal(out var d) && d > 0 ? Math.Round(d) : null;
}
