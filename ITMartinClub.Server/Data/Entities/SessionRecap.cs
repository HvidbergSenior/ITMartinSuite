namespace ITMartinClub.Server.Data.Entities;

// The generated recap itself, stored so it persists on the board/feed rather
// than being regenerated/lost.
public sealed class SessionRecap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Kind { get; set; } = "Funny"; // Funny | Practice
    public string GeneratedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
