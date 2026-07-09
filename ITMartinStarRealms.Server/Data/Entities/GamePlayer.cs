namespace ITMartinStarRealms.Server.Data.Entities;

public sealed class GamePlayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Token { get; set; } = "";   // localStorage GUID, identifies device *for this session*
    public Guid? ProfileId { get; set; }      // long-lived identity, persists across many games
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "🚀";
    public string Color { get; set; } = "#e74c3c";
    public int Points { get; set; } = 50;
    public int? Team { get; set; }            // null unless the session is a team mode
    public int SortOrder { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
