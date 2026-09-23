using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using ITMartinElPriser.Core;

namespace ITMartinLadestander.Server.Services;

// Spirii Public API v2 (docs.spirii.com): the chargers report to Spirii, so it knows every outlet's status and
// every charge, including what the resident was billed. Bearer API key created in Spirii Connect under
// Team Access -> API tokens (needs the Operator role). Read-only: the app never starts, stops or bills anything,
// so charging keeps working through Spirii's own app and charge keys even when this app is down.
public sealed class SpiriiFeed : IChargerFeed
{
    private const string Api = "https://api.spirii.com";
    private static readonly TimeZoneInfo Dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private readonly HttpClient _http;

    public SpiriiFeed(HttpClient http, string apiKey)
    {
        _http = http;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public string Name => "Spirii (live)";
    public bool IsSimulated => false;

    private static Dictionary<string, int> OutletByEvse(ForeningData f) =>
        f.OutletSetup.Where(o => o.Source == "spirii" && o.SpiriiEvseId.Length > 0)
            .ToDictionary(o => o.SpiriiEvseId.Trim(), o => o.Number, StringComparer.OrdinalIgnoreCase);

    // Every page of a cursor-paged list endpoint.
    private async Task<List<JsonElement>> ListAsync(string path, CancellationToken ct)
    {
        var all = new List<JsonElement>();
        string? cursor = null;
        for (var page = 0; page < 100; page++)
        {
            var sep = path.Contains('?') ? '&' : '?';
            var url = $"{Api}{path}{sep}limit=100" + (cursor is null ? "" : "&nextPageCursor=" + Uri.EscapeDataString(cursor));
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
            all.AddRange(doc.RootElement.GetProperty("data").EnumerateArray().Select(e => e.Clone()));
            cursor = doc.RootElement.TryGetProperty("nextPageCursor", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            if (string.IsNullOrEmpty(cursor)) break;
        }
        return all;
    }

    public async Task<List<OutletLive>> LiveAsync(ForeningData f, CancellationToken ct)
    {
        var outlets = OutletByEvse(f);
        if (outlets.Count == 0) return [];
        var evses = await ListAsync("/v2/evses", ct);
        // Ongoing charges tell who is charging and how much so far.
        var ongoing = (await ListAsync($"/v2/charge-records?ongoing=1&updatedAtFrom={Iso(DateTime.UtcNow.AddDays(-2))}", ct))
            .Where(r => !Has(r, "endedAt")).ToList();
        var list = new List<OutletLive>();
        foreach (var e in evses)
        {
            var id = Str(e, "evseId");
            if (!outlets.TryGetValue(id, out var outlet)) continue;
            var state = Str(e, "status") switch
            {
                "AVAILABLE" => OutletState.Free,
                "CHARGING" => OutletState.Charging,
                "BLOCKED" or "RESERVED" => OutletState.Connected,
                "INOPERATIVE" or "OUTOFORDER" or "REMOVED" => OutletState.Offline,
                _ => OutletState.Unknown,
            };
            var run = ongoing.FirstOrDefault(r => string.Equals(Str(r, "evseId"), id, StringComparison.OrdinalIgnoreCase));
            DateTime? since = run.ValueKind == JsonValueKind.Object && Has(run, "startedAt") ? ToDk(run.GetProperty("startedAt").GetDateTime()) : null;
            var kwh = run.ValueKind == JsonValueKind.Object ? Num(run, "consumed") : 0;
            var kw = run.ValueKind == JsonValueKind.Object && since is { } s && (DkTime.Now - s).TotalHours > 0.05 ? kwh / (DkTime.Now - s).TotalHours : 0;
            list.Add(new OutletLive(outlet, state, Math.Round(kw, 1), Math.Round(kwh, 2), since, run.ValueKind == JsonValueKind.Object ? TokenOf(run) : ""));
        }
        return list;
    }

    public async Task<List<FeedSession>> SessionsAsync(ForeningData f, DateTime fromDk, DateTime toDk, CancellationToken ct)
    {
        var outlets = OutletByEvse(f);
        var records = await ListAsync($"/v2/charge-records?updatedAtFrom={Iso(ToUtc(fromDk))}&updatedAtTo={Iso(ToUtc(toDk))}&sortDirection=asc", ct);
        var list = new List<FeedSession>();
        foreach (var r in records)
        {
            if (!Has(r, "endedAt") || !outlets.TryGetValue(Str(r, "evseId"), out var outlet)) continue;
            double? billed = r.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty("amount", out var a)
                && a.ValueKind == JsonValueKind.Number ? Math.Round(a.GetDouble(), 2) : null;
            list.Add(new FeedSession(Str(r, "transactionId") is { Length: > 0 } t ? t : Str(r, "sessionId"), outlet,
                ToDk(r.GetProperty("startedAt").GetDateTime()), ToDk(r.GetProperty("endedAt").GetDateTime()),
                Math.Round(Num(r, "consumed"), 2), TokenOf(r), billed));
        }
        return list;
    }

    // The charge key's printed label if it has one, else its UID - that is what Indstillinger matches to a household.
    private static string TokenOf(JsonElement r) =>
        r.TryGetProperty("auth", out var auth) && auth.ValueKind == JsonValueKind.Object && auth.TryGetProperty("idTag", out var tag) && tag.ValueKind == JsonValueKind.Object
            ? (Str(tag, "label") is { Length: > 0 } label ? label : Str(tag, "uid"))
            : "";

    private static bool Has(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch { JsonValueKind.String => v.GetString() ?? "", JsonValueKind.Number => v.GetRawText(), _ => "" } : "";
    private static double Num(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
    private static string Iso(DateTime utc) => Uri.EscapeDataString(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
    private static DateTime ToDk(DateTime t) => DkTime.FromUtc(t.ToUniversalTime());
    private static DateTime ToUtc(DateTime dk) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dk, DateTimeKind.Unspecified), Dk);
}
