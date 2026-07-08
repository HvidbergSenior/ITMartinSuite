namespace ITMartinStarRealms.Server.Data.Entities;

public sealed class GamePlayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Token { get; set; } = "";   // localStorage GUID, identifies device
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#e74c3c";
    public int Points { get; set; } = 50;
    public int SortOrder { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
