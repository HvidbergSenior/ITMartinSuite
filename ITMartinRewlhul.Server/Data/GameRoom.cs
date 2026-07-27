namespace ITMartinRewlhul.Server.Data;

// Plain in-memory state, not an EF entity - a party game's rooms only need
// to exist for as long as the game is being played, so there's no database
// in this app at all (see GameRoomService, which owns the room dictionary).
//
// Cooperative, not competitive: everyone in the room attempts the same
// growing sequence together each round. If every player gets it right, the
// room advances to the next level (one more pad). If anyone gets it wrong,
// the run ends for the whole group - the shared result is "how far did we
// get", not "who won".
public sealed class GameRoom
{
    public string Code { get; set; } = string.Empty;
    public List<PlayerState> Players { get; set; } = [];
    public List<int> Sequence { get; set; } = [];
    public string Phase { get; set; } = "Lobby"; // Lobby, Reveal, Attempt, GameOver
    public int Level => Sequence.Count;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
}
