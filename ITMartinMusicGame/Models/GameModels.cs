namespace ITMartinMusicGame.Models;

public enum GamePhase
{
    Lobby,
    Countdown,   // 3-2-1 before song clip plays
    Playing,     // song playing, waiting for buzz
    BuzzedIn,    // someone buzzed, 7s countdown with lyrics
    Performing,  // singer is performing (recording)
    RoundResult, // AI score shown
    GameOver     // final screen + playback
}

public class GameRoom
{
    public string Code { get; set; } = "";
    public string HostPlayerId { get; set; } = "";
    public List<Player> Players { get; set; } = [];
    public GamePhase Phase { get; set; } = GamePhase.Lobby;
    public List<CompletedRound> CompletedRounds { get; set; } = [];
    public ActiveRound? Active { get; set; }
    public List<string> UsedSongPaths { get; set; } = [];
    public const int TotalRounds = 10;
}

public class Player
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Score { get; set; }
    public bool IsHost { get; set; }
}

public class ActiveRound
{
    public int Number { get; set; }
    public string SongPath { get; set; } = "";
    public string SongTitle { get; set; } = "";
    public string? Lyrics { get; set; }
    public string? BuzzedPlayerId { get; set; }
    public string? BuzzedPlayerName { get; set; }
    public DateTimeOffset CountdownStart { get; set; }
    public DateTimeOffset? PerformanceStart { get; set; }
}

public class CompletedRound
{
    public int Number { get; set; }
    public string SongTitle { get; set; } = "";
    public string SingerName { get; set; } = "";
    public string AiTitle { get; set; } = "";
    public string AiFeedback { get; set; } = "";
    public int CommitmentScore { get; set; }
    public int PresenceScore { get; set; }
    public int PointsAwarded { get; set; }
    public string? RecordingBase64 { get; set; }
    public string RecordingMime { get; set; } = "video/webm";
    public bool HasVideo { get; set; }
}

public record GameSong(string RelativePath, string Title);
