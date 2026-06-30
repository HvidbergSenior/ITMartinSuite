namespace ITMartinClub.Server.Data.Entities;

public sealed class DocumentRead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Guid MemberId { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    public Member Member { get; set; } = null!;
}
