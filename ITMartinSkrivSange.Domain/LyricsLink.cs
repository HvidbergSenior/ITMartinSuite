namespace ITMartinSkrivSange.Domain;

// Someone else's song to practise and record (user 2026-10-19: "i need the lyrics also when recording"). The lyrics are
// not stored here (copyright) - a song named "Title (Artist)" gets a button to Ultimate Guitar, which shows the lyrics
// with the chords over them.
public static class LyricsLink
{
    /// <summary>The search for "Artist Title" on Ultimate Guitar, or null for Martin's own songs (no "(Artist)" at the end).</summary>
    public static string? For(string title)
    {
        var t = (title ?? "").Trim();
        var open = t.LastIndexOf(" (", StringComparison.Ordinal);
        if (open <= 0 || !t.EndsWith(')')) return null;
        var song = t[..open].Trim();
        var artist = t[(open + 2)..^1].Trim();
        if (song.Length == 0 || artist.Length == 0) return null;
        return "https://www.ultimate-guitar.com/search.php?search_type=title&value=" + Uri.EscapeDataString($"{artist} {song}");
    }
}
