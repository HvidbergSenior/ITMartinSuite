namespace ITMartinStarRealms.Server.Data.Entities;

public sealed class GameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public int StartingPoints { get; set; } = 50;
    public int MinPoints { get; set; } = 1;
    public int MaxPoints { get; set; } = 100;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    public Guid RulesetId { get; set; }
    public string RulesetName { get; set; } = "Standard (1v1)";
    public bool IsTeamMode { get; set; }
    public bool SharedTeamPool { get; set; }
    public bool IsCompleted { get; set; }

    // Setup (invite screen, picking names/avatars/colors) is separate from real
    // play - point adjustments before this flips true don't count as game
    // events, so accidental taps while showing people around don't pollute
    // the log or a player's real point total.
    public bool HasStarted { get; set; }

    public List<GamePlayer> Players { get; set; } = [];
}
