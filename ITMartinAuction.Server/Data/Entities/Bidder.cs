namespace ITMartinAuction.Server.Data.Entities;

public sealed class Bidder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#f5a623";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
