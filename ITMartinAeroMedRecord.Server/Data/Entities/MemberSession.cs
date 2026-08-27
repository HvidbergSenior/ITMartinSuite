namespace ITMartinAeroMedRecord.Server.Data.Entities;

public sealed class MemberSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);

    public Member Member { get; set; } = null!;
}
