namespace ITMartinMusikStudio.Server.Data.Entities;

public class StudioSong
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string MusicKey { get; set; } = "C";
    public int? Tempo { get; set; }
    public string Lyrics { get; set; } = "";
    public string ChordChart { get; set; } = "";
    public string Notes { get; set; } = "";
    public string FingerpickPattern { get; set; } = "";
    public string StrumPattern { get; set; } = "";

    // Linked Spotify track (see SpotifyService) - lets the song be played via
    // the Web Playback SDK for reference listening / as an overdub backing
    // track, without needing a locally uploaded SourceFile.
    public string? SpotifyTrackId { get; set; }
    public string? SpotifyTrackLabel { get; set; } // "Name — Artist", cached so the UI doesn't re-fetch just to display it

    // Cached lrclib.net result (raw LRC text, "[mm:ss.xx]line" per line) so a
    // song's lyrics view doesn't hit lrclib.net on every page load. Null means
    // "not looked up yet", empty string means "looked up, none found".
    public string? SyncedLyrics { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
