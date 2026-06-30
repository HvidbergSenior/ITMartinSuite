namespace ITMartinClub.Server.Data.Entities;

public sealed class MemberSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Member Member { get; set; } = null!;
}
