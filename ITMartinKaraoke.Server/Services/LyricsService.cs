using System.Text.Json.Serialization;

namespace ITMartinKaraoke.Server.Services;

// Spotify's public API doesn't expose lyrics at all - lrclib.net is a free,
// no-auth, community-sourced alternative that returns real time-synced (LRC
// format) lyrics, which is what makes karaoke-style line highlighting
// possible for both Spotify tracks and ripped-CD files (looked up by
// title/artist either way).
public sealed class LyricsService
{
    private const string SearchUrl = "https://lrclib.net/api/search";

    private readonly IHttpClientFactory _httpFactory;

    public LyricsService(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<(string Synced, string Plain)> FindLyricsAsync(string title, string artist, int? targetDurationMs = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return ("", "");

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ITMartinKaraoke/1.0");

        var url = $"{SearchUrl}?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
        var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return ("", "");

        var results = await resp.Content.ReadFromJsonAsync<List<LrcLibResult>>(cancellationToken: ct);
        var candidates = results?.Where(r => !string.IsNullOrWhiteSpace(r.SyncedLyrics) || !string.IsNullOrWhiteSpace(r.PlainLyrics)).ToList();
        if (candidates is null || candidates.Count == 0) return ("", "");

        var best = targetDurationMs is int targetMs
            ? candidates.OrderBy(r => Math.Abs((r.Duration ?? 0) * 1000 - targetMs)).First()
            : candidates.First();

        return (best.SyncedLyrics ?? "", best.PlainLyrics ?? "");
    }

    // Parses "[mm:ss.xx]line" into (seconds, text) pairs, ordered by time.
    public static List<(double Seconds, string Text)> ParseLrc(string lrc)
    {
        var lines = new List<(double, string)>();
        if (string.IsNullOrWhiteSpace(lrc)) return lines;

        foreach (var rawLine in lrc.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length < 10 || line[0] != '[') continue;

            var close = line.IndexOf(']');
            if (close < 0) continue;

            var tag = line[1..close];
            var text = line[(close + 1)..].Trim();
            var parts = tag.Split(':');
            if (parts.Length != 2) continue;

            if (int.TryParse(parts[0], out var minutes) &&
                double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            {
                lines.Add((minutes * 60 + seconds, text));
            }
        }

        return lines.OrderBy(l => l.Item1).ToList();
    }

    private sealed class LrcLibResult
    {
        [JsonPropertyName("syncedLyrics")] public string? SyncedLyrics { get; set; }
        [JsonPropertyName("plainLyrics")] public string? PlainLyrics { get; set; }
        [JsonPropertyName("duration")] public double? Duration { get; set; }
    }
}
