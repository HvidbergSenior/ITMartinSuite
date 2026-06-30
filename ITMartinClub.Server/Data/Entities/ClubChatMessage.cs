namespace ITMartinClub.Server.Data.Entities;

public sealed class ClubChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid MemberId { get; set; }
    public string SenderName { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
