using System.Net.Http.Headers;
using System.Text.Json;
using ITMartinElPriser.Core;

namespace ITMartinLadestander.Server.Services;

// Zaptec's public API (docs.zaptec.com): OAuth password grant with the association's Zaptec Portal login, live
// state per charger and the archived sessions with kWh and RFID tag. Each Zaptec Pro has one outlet, so a charger
// id maps to one outlet in OutletSetup. Read-only for now - no commands are sent to the chargers.
public sealed class ZaptecFeed : IChargerFeed
{
    private const string Api = "https://api.zaptec.com";
    // State ids from Zaptec's /api/constants (verify on the first real connection).
    private const int ChargerOperationMode = 710; // 1 disconnected, 2 connected/waiting, 3 charging, 5 finished
    private const int TotalChargePower = 513;      // W
    private const int SessionEnergy = 553;         // kWh in the current session

    private readonly HttpClient _http;
    private readonly string _user, _password, _installationId;
    private string? _token;
    private DateTime _tokenExpiresUtc;

    public ZaptecFeed(HttpClient http, string user, string password, string installationId)
    {
        _http = http; _user = user; _password = password; _installationId = installationId;
    }

    public string Name => "Zaptec (live)";
    public bool IsSimulated => false;

    private async Task AuthorizeAsync(CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow < _tokenExpiresUtc) return;
        using var res = await _http.PostAsync($"{Api}/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["username"] = _user, ["password"] = _password, ["scope"] = "openid",
        }), ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        _token = doc.RootElement.GetProperty("access_token").GetString();
        _tokenExpiresUtc = DateTime.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 120);
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        await AuthorizeAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, Api + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
    }

    private static Dictionary<string, int> OutletByCharger(ForeningData f) =>
        f.OutletSetup.Where(o => o.Source == "zaptec" && o.ZaptecChargerId.Length > 0)
            .ToDictionary(o => o.ZaptecChargerId, o => o.Number, StringComparer.OrdinalIgnoreCase);

    public async Task<List<OutletLive>> LiveAsync(ForeningData f, CancellationToken ct)
    {
        var list = new List<OutletLive>();
        foreach (var (chargerId, outlet) in OutletByCharger(f))
        {
            using var doc = await GetAsync($"/api/chargers/{chargerId}/state", ct);
            string V(int id) => doc.RootElement.EnumerateArray().FirstOrDefault(s => s.GetProperty("stateId").GetInt32() == id) is { ValueKind: JsonValueKind.Object } s
                && s.TryGetProperty("valueAsString", out var v) ? v.GetString() ?? "" : "";
            static double D(string s) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
            var state = V(ChargerOperationMode) switch
            {
                "1" => OutletState.Free, "2" => OutletState.Connected, "3" => OutletState.Charging, "5" => OutletState.Finished, _ => OutletState.Unknown,
            };
            list.Add(new OutletLive(outlet, state, Math.Round(D(V(TotalChargePower)) / 1000, 1), Math.Round(D(V(SessionEnergy)), 2), null, ""));
        }
        return list;
    }

    public async Task<List<FeedSession>> SessionsAsync(ForeningData f, DateTime fromDk, DateTime toDk, CancellationToken ct)
    {
        var outlets = OutletByCharger(f);
        var list = new List<FeedSession>();
        string? cursor = null;
        for (var page = 0; page < 50; page++)
        {
            var q = $"/api/sessions/archived?InstallationId={_installationId}&From={Utc(fromDk):o}&To={Utc(toDk):o}&PageSize=200"
                    + (cursor is null ? "" : "&Cursor=" + Uri.EscapeDataString(cursor));
            using var doc = await GetAsync(q, ct);
            var root = doc.RootElement;
            foreach (var s in root.GetProperty("sessions").EnumerateArray())
            {
                if (s.TryGetProperty("voided", out var voided) && voided.ValueKind == JsonValueKind.True) continue;
                var charger = s.GetProperty("chargerId").GetString() ?? "";
                if (!outlets.TryGetValue(charger, out var outlet)) continue;
                if (s.GetProperty("endDateTime").ValueKind != JsonValueKind.String) continue;
                var token = s.TryGetProperty("tokenName", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
                if (token.Length == 0 && s.TryGetProperty("authorizedUser", out var u) && u.ValueKind == JsonValueKind.Object
                    && u.TryGetProperty("fullName", out var n) && n.ValueKind == JsonValueKind.String) token = n.GetString() ?? "";
                list.Add(new FeedSession(s.GetProperty("id").GetString() ?? "", outlet,
                    DkTime.FromUtc(s.GetProperty("startDateTime").GetDateTime().ToUniversalTime()),
                    DkTime.FromUtc(s.GetProperty("endDateTime").GetDateTime().ToUniversalTime()),
                    Math.Round(s.GetProperty("energy").GetDouble(), 2), token));
            }
            if (!root.TryGetProperty("hasMore", out var more) || more.ValueKind != JsonValueKind.True) break;
            cursor = root.GetProperty("cursor").GetString();
        }
        return list;
    }

    private static readonly TimeZoneInfo Dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private static DateTime Utc(DateTime dk) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dk, DateTimeKind.Unspecified), Dk);
}
