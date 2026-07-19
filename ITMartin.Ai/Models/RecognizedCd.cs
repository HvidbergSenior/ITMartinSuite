namespace ITMartin.Ai.Models;

public sealed record RecognizedCd
{
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";

    // From a visible back-cover tracklist when photographed, or - if only the
    // front cover is shown - Claude's own knowledge of a well-known album's
    // tracklist. Either way this is just a starting point: every track still
    // goes through the same Jamendo/ccMixter lookup afterward, so a wrong or
    // approximate title just results in "not found" rather than a false
    // positive - it never itself decides whether a song is cleared.
    public List<string> Tracks { get; init; } = [];
}
