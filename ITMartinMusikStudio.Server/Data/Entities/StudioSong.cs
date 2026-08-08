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

    // CSV of step Keys (see StepPlanner) the user explicitly marked "not
    // necessary" for this song. Everything else about step state (Done vs
    // NotStarted) is inferred from the fields above - this is the only
    // step-completion state that needs persisting.
    public string SkippedSteps { get; set; } = "";

    // Relative path (under StudioLibraryService.Root) to a generated cover
    // image, empty if none. Deliberately NOT part of the step checklist -
    // it's decoration, not something needed to actually record the song, so
    // it doesn't gate Indspil the way chords/lyrics/pattern do.
    public string CoverImagePath { get; set; } = "";

    // Where each lyric section starts in SourceFile, in seconds - lets
    // "record with the song in the background" seek straight to a section's
    // own spot instead of always starting from 0:00. Plain "slug=seconds;..."
    // pairs (slug = same letters/digits-only key used in take filenames,
    // e.g. "chorus"), not JSON - consistent with this entity's other
    // lightweight delimited-string fields (SkippedSteps). Null/empty means
    // no timings set yet for any section.
    public string? SectionTimings { get; set; }

    // Which beat (absolute count from song start, 1-based bar-of-4 counting
    // continues across bars) each lyric line starts on - "so I know when to
    // come in". Keyed by the line's plain 0-based index in Lyrics.Split('\n')
    // (not by line text, since this song repeats short lines like "Jeg
    // kigger på stjerner." within the same section). Same "slug=seconds"-
    // style delimited string as SectionTimings, not JSON.
    public string? LineBeats { get; set; }
}
