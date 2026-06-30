namespace ITMartinFamily.Domain.Entities;

public sealed class FamilySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
