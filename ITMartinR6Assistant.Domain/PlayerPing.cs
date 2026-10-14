namespace ITMartinR6Assistant.Domain;

// "Her er jeg" on the map, shared with the team (user 2026-10-13: everyone sees each other's dot, each in their own colour).
// Live only - kept in memory, gone after a restart, and hidden once it is older than MaxAge.
public sealed record PlayerPing(string Player, string Color, string Map, string Floor, double X, double Y, DateTimeOffset AtUtc)
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(90);
    public bool IsFresh(DateTimeOffset now) => now - AtUtc < MaxAge;
}
