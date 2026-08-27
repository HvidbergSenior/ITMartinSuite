namespace ITMartinClub.Server.Data.Entities;

public sealed class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }

    // Free-text, not an enum - vocabulary differs per group (Leder/Medlem for a
    // club, Ejer/Medarbejder for a shop, Selv/Støtte for the ADHD use case).
    // Purely a label for now: shown on Medlemmer, set by admin - no permission
    // gating tied to it yet.
    public string? Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public List<MemberSession> Sessions { get; set; } = [];
    public List<BulletinPost> Posts { get; set; } = [];
}
