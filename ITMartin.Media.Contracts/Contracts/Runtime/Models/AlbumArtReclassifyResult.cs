namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class AlbumArtReclassifyResult
{
    public int Checked { get; init; }
    public int MovedHighConfidence { get; init; }

    // Filename-only matches (cover/front/back/folder etc.) without the
    // unambiguous "AlbumArt <GUID> Large" cache-file signature - not moved
    // automatically, since a real photo can legitimately be named "front.jpg"
    // or "...Cover.jpg" (see Skiing-Facebook-Profile-Timeline-Cover.jpg on
    // mie's real library, 2026-08-25 - a Facebook cover photo, not album art).
    public List<string> ReviewCandidates { get; init; } = [];
}
