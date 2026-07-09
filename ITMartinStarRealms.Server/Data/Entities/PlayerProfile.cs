namespace ITMartinStarRealms.Server.Data.Entities;

// A long-lived, device-scoped identity (localStorage key distinct from the
// per-session join token) so the same person is recognized across many
// separate games, enabling real win/loss history instead of only ever
// seeing stats for a single session.
public sealed class PlayerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceToken { get; set; } = "";
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "🚀";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
