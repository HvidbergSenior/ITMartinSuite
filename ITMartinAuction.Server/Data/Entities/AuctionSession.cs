namespace ITMartinAuction.Server.Data.Entities;

public sealed class AuctionSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AuctionStatus Status { get; set; } = AuctionStatus.Draft;
    public string AdminToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime? AuctionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public Guid? ActiveItemId { get; set; }

    public List<AuctionItem> Items { get; set; } = [];
    public List<Bidder> Bidders { get; set; } = [];
    public List<ChatMessage> ChatMessages { get; set; } = [];
}

public enum AuctionStatus { Draft, PreAuction, Live, Ended }
