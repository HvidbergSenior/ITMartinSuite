namespace ITMartinClub.Domain;

public sealed class MemberSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(365);

    public Member Member { get; set; } = null!;

    /// <summary>Not expired and belongs to this team (needs Member.Group loaded).</summary>
    public bool IsValidFor(string slug, DateTime nowUtc) => Member.Group.Slug == slug && ExpiresAt >= nowUtc;

    /// <summary>Sliding: anyone who shows up keeps their login another year. True if it moved.</summary>
    public bool Slide(DateTime nowUtc)
    {
        if (ExpiresAt >= nowUtc.AddDays(300)) return false;
        ExpiresAt = nowUtc.AddDays(365);
        return true;
    }
}
