namespace ITMartinBarTab.Server.Data.Entities;

public sealed class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(3);

    public List<Participant> Participants { get; set; } = [];
    public List<DrinkEntry> Drinks { get; set; } = [];
    public List<ChatMessage> Messages { get; set; } = [];
}
