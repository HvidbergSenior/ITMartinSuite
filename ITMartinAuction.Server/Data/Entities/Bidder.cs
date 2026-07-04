namespace ITMartinAuction.Server.Data.Entities;

public sealed class Bidder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Token { get; set; } = "";         // localStorage GUID, identifies device
    public string Name { get; set; } = "";           // real name, only admin sees this
    public string? Phone { get; set; }
    public int? BidderNumber { get; set; }           // public anonymous number (Budgiver #3)
    public string Color { get; set; } = "#f5a623";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
