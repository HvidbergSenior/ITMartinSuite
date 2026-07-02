namespace ITMartinBarTab.Server.Data.Entities;

public sealed class Participant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#4f8ef7";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Session Session { get; set; } = null!;
    public List<DrinkShare> Shares { get; set; } = [];
    public List<ChatMessage> Messages { get; set; } = [];
}
