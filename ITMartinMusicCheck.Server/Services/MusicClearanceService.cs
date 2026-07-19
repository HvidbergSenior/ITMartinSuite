using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITMartinMusicCheck.Server.Services;

public sealed record ClearanceMatch(string Source, string TrackName, string ArtistName, string LicenseName, string? LicenseUrl, string? PageUrl);

// Deliberately narrow: this can only ever give a POSITIVE match against
// catalogs that are actually free-to-use by design (Jamendo, ccMixter). For
// anything not found there - which is essentially all normal commercial
// music - the honest answer is "unknown, assume it needs a real license",
// never a false "this looks fine". There is no database anywhere that can
// tell you a random commercial CD track is cleared for sharing; only known
// royalty-free/Creative-Commons catalogs can be confirmed programmatically.
public sealed class MusicClearanceService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string? _jamendoClientId;

    public MusicClearanceService(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _jamendoClientId = config["Jamendo:ClientId"];
    }

    public async Task<List<ClearanceMatch>> CheckAsync(string title, string artist, CancellationToken ct = default)
    {
        var matches = new List<ClearanceMatch>();

        var jamendoTask = CheckJamendoAsync(title, artist, ct);
        var ccMixterTask = CheckCcMixterAsync(title, ct);
        await Task.WhenAll(jamendoTask, ccMixterTask);

        matches.AddRange(jamendoTask.Result);
        matches.AddRange(ccMixterTask.Result);
        return matches;
    }

    private async Task<List<ClearanceMatch>> CheckJamendoAsync(string title, string artist, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_jamendoClientId)) return [];

        try
        {
            var http = _httpFactory.CreateClient();
            var url = $"https://api.jamendo.com/v3.0/tracks/?client_id={Uri.EscapeDataString(_jamendoClientId)}" +
                      $"&format=json&limit=5&namesearch={Uri.EscapeDataString(title)}" +
                      (string.IsNullOrWhiteSpace(artist) ? "" : $"&artist_name={Uri.EscapeDataString(artist)}");

            var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return [];

            var doc = await resp.Content.ReadFromJsonAsync<JamendoResponse>(cancellationToken: ct);
            return doc?.Results?.Select(r => new ClearanceMatch(
                "Jamendo",
                r.Name ?? title,
                r.ArtistName ?? artist,
                "Creative Commons",
                r.LicenseCcurl,
                r.Shareurl)).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<ClearanceMatch>> CheckCcMixterAsync(string title, CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient();
            var url = $"https://ccmixter.org/api/query?f=json&search={Uri.EscapeDataString(title)}&search_type=any&limit=5";

            var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return [];

            var results = await resp.Content.ReadFromJsonAsync<List<CcMixterResult>>(cancellationToken: ct);
            return results?.Select(r => new ClearanceMatch(
                "ccMixter",
                r.UploadName ?? title,
                r.UserRealName ?? r.UserName ?? "ukendt",
                r.LicenseName ?? "Creative Commons",
                r.LicenseUrl,
                r.FilePageUrl)).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed class JamendoResponse
    {
        [JsonPropertyName("results")] public List<JamendoTrack>? Results { get; set; }
    }

    private sealed class JamendoTrack
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("artist_name")] public string? ArtistName { get; set; }
        [JsonPropertyName("license_ccurl")] public string? LicenseCcurl { get; set; }
        [JsonPropertyName("shareurl")] public string? Shareurl { get; set; }
    }

    private sealed class CcMixterResult
    {
        [JsonPropertyName("upload_name")] public string? UploadName { get; set; }
        [JsonPropertyName("user_name")] public string? UserName { get; set; }
        [JsonPropertyName("user_real_name")] public string? UserRealName { get; set; }
        [JsonPropertyName("license_name")] public string? LicenseName { get; set; }
        [JsonPropertyName("license_url")] public string? LicenseUrl { get; set; }
        [JsonPropertyName("file_page_url")] public string? FilePageUrl { get; set; }
    }
}
