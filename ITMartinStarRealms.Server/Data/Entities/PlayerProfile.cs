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

    // SHA256 hash of an optional short PIN - protects *playing as* this
    // identity from a device that doesn't already own it, not read access
    // (stats/leaderboard stay fully public regardless of this). Never
    // serialized out - clients only ever need to know HasPin, not the hash.
    [System.Text.Json.Serialization.JsonIgnore]
    public string? PinHash { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool HasPin => !string.IsNullOrEmpty(PinHash);
}
