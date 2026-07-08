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

    public List<GamePlayer> Players { get; set; } = [];
}
