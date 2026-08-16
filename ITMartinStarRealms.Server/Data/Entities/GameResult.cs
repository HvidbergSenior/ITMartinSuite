namespace ITMartinStarRealms.Server.Data.Entities;

// Permanent record of a completed game - kept forever, unlike GameSession/
// GamePlayer which CleanupService purges after ExpiresAt. This is what
// stats/history queries read from.
public sealed class GameResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string RulesetName { get; set; } = "";
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    // Copied from GameSession.IsRanked at completion time (not joined back to
    // Session, which CleanupService purges) - stats/leaderboard queries filter
    // on this so training games never count.
    public bool IsRanked { get; set; } = true;

    public List<GameResultPlayer> Players { get; set; } = [];
}

public sealed class GameResultPlayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameResultId { get; set; }
    public Guid? ProfileId { get; set; } // null if that device never set up a profile
    public string Name { get; set; } = "";
    public int FinalPoints { get; set; }
    public bool IsWinner { get; set; }
    public int? Team { get; set; }

    // Set only for ranked team-mode games where every player on this side had
    // a real profile - resolved once, at game-finish time, via
    // GameService.GetOrCreateTeamAsync.
    public Guid? TeamId { get; set; }
}
