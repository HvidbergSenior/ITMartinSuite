namespace ITMartinKaraoke.Server.Data.Entities;

public class QueueEntry
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string SingerName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";

    // Exactly one of these identifies where the audio comes from.
    public string? SpotifyTrackId { get; set; }
    public string? SourceFile { get; set; } // relative path under the ripped-CD library folder

    public string? SyncedLyrics { get; set; }
    public string? PlainLyrics { get; set; }

    // Queued -> Playing -> Done (advanced by the TV/Stage view)
    public string Status { get; set; } = "Queued";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Set once a performance recording has been uploaded for this entry.
    // Multiple phones can each record their own mic during the same
    // performance - they are never live-mixed, just saved side by side and
    // named after whoever recorded them (see RecordingFile naming in
    // Program.cs) so they can be combined by hand afterward if wanted.
    public string? RecordingFile { get; set; }

    // Broadcast periodically by the Stage/TV view while a track plays, so
    // phones following along (Follow page) can highlight the same lyric
    // line without needing their own Spotify/local playback connection.
    public int? PositionMs { get; set; }
    public DateTime? PositionUpdatedAt { get; set; }

    // A fun AI-generated "concert poster" for this performance - set on
    // request from the Remote page, shown on the Stage/TV view. Optional
    // and per-performance (never generated automatically for every queue
    // add), keeping API spend proportional to actual use.
    public string? PosterUrl { get; set; }
}
