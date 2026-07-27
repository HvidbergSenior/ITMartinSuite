namespace ITMartinMusic.Server.Data.Entities;

// One row per song key shown on the public listener - separate from the
// unrelated personal practice-journal "Song" entity (Library/SongDetail
// pages), which predates this and has its own Guid-keyed model.
public sealed class ListenerSong
{
    public string SongKey { get; set; } = "";
    public string? Category { get; set; } // "Own" | "Known" | null (unset)
}
