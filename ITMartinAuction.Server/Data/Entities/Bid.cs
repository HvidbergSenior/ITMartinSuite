namespace ITMartinAuction.Server.Data.Entities;

public sealed class Bid
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuctionItemId { get; set; }
    public Guid BidderId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;

    public Bidder? Bidder { get; set; }
}
