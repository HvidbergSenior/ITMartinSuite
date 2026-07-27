namespace ITMartinClub.Server.Data.Entities;

// Quick one-liner notes players add after a game session ("Peter was on
// fire", "Martin died too much but laughed") - collected across all
// contributing members and fed to ClubAiService to generate the funny recap.
public sealed class SessionNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool UsedInRecap { get; set; }
}
