namespace ITMartinAuction.Server.Data.Entities;

public sealed class AuctionSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(3);
    public Guid? ActiveItemId { get; set; }

    public List<AuctionItem> Items { get; set; } = [];
    public List<Bidder> Bidders { get; set; } = [];
}
