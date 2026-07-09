namespace ITMartinStarRealms.Server.Data.Entities;

public sealed class GameRuleset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int MinPlayers { get; set; } = 2;
    public int MaxPlayers { get; set; } = 6;
    public bool IsTeamMode { get; set; }
    public int PlayersPerTeam { get; set; } // 0 when not a team mode
    public bool SharedTeamPool { get; set; } // true = teammates share one Authority total (Hydra); false = individual per player, team is eliminated once every member is out
    public int DefaultStartingPoints { get; set; } = 50;
    public bool IsBuiltIn { get; set; }
    public string CreatedByProfileName { get; set; } = ""; // who made it, if custom
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
