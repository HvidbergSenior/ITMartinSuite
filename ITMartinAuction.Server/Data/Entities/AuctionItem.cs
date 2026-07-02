namespace ITMartinAuction.Server.Data.Entities;

public sealed class AuctionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal StartingPrice { get; set; }
    public string? PhotoPath { get; set; }
    public AuctionItemStatus Status { get; set; } = AuctionItemStatus.Pending;
    public int SortOrder { get; set; }
    public Guid? WinnerBidderId { get; set; }
    public decimal? WinningBid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Bid> Bids { get; set; } = [];
}

public enum AuctionItemStatus { Pending, Active, Sold, Passed }
