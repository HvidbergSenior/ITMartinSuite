using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace ITMartinLager.Server.Services;

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
            // Their search answers 500 on some "Nr. 3" searches (2026-10-07: "Tintin Nr 3" fails, "Tintin 3" works),
            // so "Nr." is left out; if it still fails, once more with the words only.
            var json = await SearchAsync(NrRx().Replace(query, " "), ct)
                       ?? await SearchAsync(string.Join(' ', ListingParse.Words(query).Where(w => !ListingParse.IsNumber(w))), ct);
            if (json is null) { log.LogInformation("Faraos search failed for {Query}", query); return null; }
            var result = Parse(json, title, author, SearchUrl(query));
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

    private async Task<string?> SearchAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var body = JsonSerializer.Serialize(new { inSearchDialog = true, searchString = text.Trim(), tag = (string?)null, sortOrder = 5, subPage = 0, page = 1 });
        using var req = new HttpRequestMessage(HttpMethod.Post, Api) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        req.Headers.TryAddWithoutValidation("Origin", "https://www.faraos.dk");
        req.Headers.UserAgent.ParseAdd("BogshoppenLager/1.0 (ITMartin@Mensa.dk)");
        using var res = await http.SendAsync(req, ct);
        return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync(ct) : null;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\bnr\.?\s*", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex NrRx();

    internal static Listings Parse(string json, string title, string author, string url)
    {
        var match = ListingParse.Matcher(title, author) with { Surname = "" };   // comics carry no author in the name
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
                            used.Add(new Offer($"{name} ({Str(ed, "editionName")}, {Str(c, "name")})", kr, link));
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
