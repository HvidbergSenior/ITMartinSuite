namespace ITMartinStarRealms.Server.Data.Entities;

// A specific recurring group of players (e.g. "ITMartin + Eigil") that can be
// given a custom name (e.g. "The Fighters"). Identity is the exact member
// set, not any one game - the same pairing always resolves back to the same
// Team row across many separate games/sessions.
public sealed class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Sorted, "|"-joined ProfileIds - order-independent so it doesn't matter
    // who joined the session first.
    public string MemberKey { get; set; } = "";

    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
