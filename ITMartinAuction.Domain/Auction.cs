namespace ITMartinAuction.Domain;

// Live Auktion: an auction (session) with items; bidders bid from their phones, the auctioneer runs it from Admin.

public sealed class AuctionSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AuctionStatus Status { get; set; } = AuctionStatus.Draft;
    public string AdminToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime? AuctionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);   // CleanupJob deletes it then
    public Guid? ActiveItemId { get; set; }

    public List<AuctionItem> Items { get; set; } = [];
    public List<Bidder> Bidders { get; set; } = [];
    public List<ChatMessage> ChatMessages { get; set; } = [];
}

public enum AuctionStatus { Draft, PreAuction, Live, Ended }

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
    public int LotQuantity { get; set; } = 1;
    public Guid? WinnerBidderId { get; set; }
    public decimal? WinningBid { get; set; }
    public string? BuyNowBuyerName { get; set; }
    public string? BuyNowBuyerPhone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // From Bogshoppen Lager (2026-10-12, user: "put interesting and bronze, silver, gold on liveauctions... that should
    // be fun"): "🏆 GULD – sælges for ca. 1.500 kr andre steder", "🥈 SØLV …", "🥉 BRONZE …", "🔎 MÅSKE …". Null = ordinary.
    public string? Highlight { get; set; }

    public List<Bid> Bids { get; set; } = [];

    public bool IsHighlighted => !string.IsNullOrWhiteSpace(Highlight);
}

public enum AuctionItemStatus { Pending, Active, Sold, Passed }

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

public sealed class Bid
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuctionItemId { get; set; }
    public Guid BidderId { get; set; }
    public decimal Amount { get; set; }
    public bool IsPreBid { get; set; }
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;

    public Bidder? Bidder { get; set; }
}

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int BidderNumber { get; set; }
    public string Message { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
