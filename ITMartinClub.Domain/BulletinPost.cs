namespace ITMartinClub.Server.Data.Entities;

public sealed class BulletinPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid MemberId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public string? ImageFileName { get; set; }
    public string Tag { get; set; } = "General"; // General, Game, Personal

    public Group Group { get; set; } = null!;
    public Member Member { get; set; } = null!;
}
