namespace ITMartinBarTab.Server.Data.Entities;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid? DrinkEntryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Session Session { get; set; } = null!;
    public Participant Participant { get; set; } = null!;
}
