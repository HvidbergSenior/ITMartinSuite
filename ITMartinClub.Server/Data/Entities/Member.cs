namespace ITMartinClub.Server.Data.Entities;

public sealed class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public List<MemberSession> Sessions { get; set; } = [];
    public List<DocumentRead> DocumentReads { get; set; } = [];
    public List<BulletinPost> Posts { get; set; } = [];
}
