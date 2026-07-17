using System.Text.Json.Serialization;

namespace ITMartinMusikStudio.Server.Services;

// Spotify's public API doesn't expose lyrics at all (their in-app lyrics come
// from a licensed source they don't share with developers) - lrclib.net is a
// free, no-auth, community-sourced alternative that returns real time-synced
// (LRC format) lyrics, which is what makes karaoke-style line highlighting
// possible.
public sealed class LyricsService
{
    private const string SearchUrl = "https://lrclib.net/api/search";

    private readonly IHttpClientFactory _httpFactory;

    public LyricsService(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    // Synced: raw LRC text ("[mm:ss.xx]line" per line), for the karaoke-style
    // highlighter. Plain: the same lyrics with timestamps stripped, for the
    // existing free-text "Tekst / Lyrics" field elsewhere in the app - lrclib
    // returns both directly, no need to derive one from the other.
    // Either can be "" if nothing was found (not null - null is reserved by
    // the caller for "never looked up").
    //
    // targetDurationMs (usually the linked Spotify track's own duration) picks
    // the right take when lrclib has both a studio and a live/alternate entry -
    // search text alone ("Song - Live") isn't reliable since lrclib ranks by
    // its own relevance, not by matching our version qualifier. A live take is
    // almost always a noticeably different length than the studio one, so the
    // closest-duration result is a much safer signal than "just take the first
    // hit with any lyrics."
    public async Task<(string Synced, string Plain)> FindLyricsAsync(string title, string artist, int? targetDurationMs = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return ("", "");

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ITMartinMusikStudio/1.0");

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

    // Parses "[mm:ss.xx]line" (or "[mm:ss.xxx]") into (seconds, text) pairs,
    // ordered by time - what the client-side highlighter needs.
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
        [JsonPropertyName("duration")] public double? Duration { get; set; } // seconds
    }
}
