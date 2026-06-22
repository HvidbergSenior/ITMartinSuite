using ITMartinAuction.Server.Data;
using ITMartinAuction.Server.Data.Entities;
using ITMartinAuction.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAuction.Server.Services;

public sealed class AuctionService(AuctionDbContext db, IHubContext<AuctionHub> hub)
{
    private static readonly string[] Colors =
    [
        "#f5a623", "#3498db", "#2ecc71", "#e74c3c",
        "#9b59b6", "#1abc9c", "#e67e22", "#e91e63"
    ];

    public async Task<AuctionSession> CreateAsync(string name)
    {
        var session = new AuctionSession { Name = name, Code = GenerateCode() };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public Task<AuctionSession?> GetByCodeAsync(string code) =>
        db.Sessions
            .Include(s => s.Bidders)
            .Include(s => s.Items)
                .ThenInclude(i => i.Bids)
                    .ThenInclude(b => b.Bidder)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper());

    public async Task<Bidder> JoinAsync(string code, string name)
    {
        var session = await db.Sessions
            .Include(s => s.Bidders)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session not found");

        // Return existing bidder if same name
        var existing = session.Bidders.FirstOrDefault(b =>
            string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        var color = Colors[session.Bidders.Count % Colors.Length];
        var bidder = new Bidder { SessionId = session.Id, Name = name, Color = color };
        db.Bidders.Add(bidder);
        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("BidderJoined", new
        {
            bidder.Id, bidder.Name, bidder.Color
        });

        return bidder;
    }

    public async Task<AuctionItem> AddItemAsync(
        string code, string name, string? description,
        decimal startingPrice, string? photoPath)
    {
        var session = await db.Sessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session not found");

        var item = new AuctionItem
        {
            SessionId  = session.Id,
            Name       = name,
            Description = description,
            StartingPrice = startingPrice,
            PhotoPath  = photoPath,
            SortOrder  = session.Items.Count
        };

        db.Items.Add(item);
        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("ItemAdded", new
        {
            item.Id, item.Name, item.Description,
            item.StartingPrice, item.PhotoPath,
            Status = item.Status.ToString(), item.SortOrder
        });

        return item;
    }

    public async Task ActivateItemAsync(string code, Guid itemId)
    {
        var session = await db.Sessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session not found");

        // Deactivate any currently active item
        foreach (var active in session.Items.Where(i => i.Status == AuctionItemStatus.Active))
            active.Status = AuctionItemStatus.Pending;

        var item = session.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item not found");

        item.Status = AuctionItemStatus.Active;
        session.ActiveItemId = itemId;
        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("ItemActivated", new
        {
            item.Id, item.Name, item.Description,
            item.StartingPrice, item.PhotoPath
        });
    }

    public async Task PlaceBidAsync(string code, Guid itemId, Guid bidderId, decimal amount)
    {
        var item = await db.Items
            .Include(i => i.Bids)
            .FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item not found");

        if (item.Status != AuctionItemStatus.Active)
            throw new InvalidOperationException("Item is not active");

        var minBid = item.Bids.Any()
            ? item.Bids.Max(b => b.Amount) + 1
            : item.StartingPrice;

        if (amount < minBid)
            throw new InvalidOperationException($"Minimum bid is {minBid:0} kr");

        var bidder = await db.Bidders.FindAsync(bidderId)
            ?? throw new InvalidOperationException("Bidder not found");

        var bid = new Bid { AuctionItemId = itemId, BidderId = bidderId, Amount = amount };
        db.Bids.Add(bid);
        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("BidPlaced", new
        {
            bid.Id, ItemId = itemId,
            bid.BidderId, bidder.Name, bidder.Color,
            bid.Amount, bid.PlacedAt
        });
    }

    public async Task CloseItemAsync(string code, Guid itemId, bool sold)
    {
        var item = await db.Items
            .Include(i => i.Bids)
                .ThenInclude(b => b.Bidder)
            .FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item not found");

        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session not found");

        Bidder? winner = null;

        if (sold && item.Bids.Any())
        {
            var topBid = item.Bids.OrderByDescending(b => b.Amount).First();
            item.WinnerBidderId = topBid.BidderId;
            item.WinningBid    = topBid.Amount;
            item.Status        = AuctionItemStatus.Sold;
            winner = topBid.Bidder;
        }
        else
        {
            item.Status = AuctionItemStatus.Passed;
        }

        if (session.ActiveItemId == itemId)
            session.ActiveItemId = null;

        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("ItemClosed", new
        {
            ItemId    = itemId,
            Status    = item.Status.ToString(),
            WinnerId  = item.WinnerBidderId,
            WinnerName  = winner?.Name,
            WinnerColor = winner?.Color,
            WinningBid  = item.WinningBid
        });
    }

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}
