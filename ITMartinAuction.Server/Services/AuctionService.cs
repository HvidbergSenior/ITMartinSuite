using ITMartinAuction.Server.Data;
using ITMartinAuction.Server.Data.Entities;
using ITMartinAuction.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAuction.Server.Services;

public sealed class AuctionService(
    AuctionDbContext db,
    IHubContext<AuctionHub> hub,
    CountdownService countdown)
{
    private static readonly string[] Colors =
    [
        "#f5a623", "#3498db", "#2ecc71", "#e74c3c",
        "#9b59b6", "#1abc9c", "#e67e22", "#e91e63"
    ];

    // ── Session queries ─────────────────────────────────────────────────

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

    public async Task<AuctionSession?> GetAdminSessionAsync(string code, string adminToken)
    {
        var session = await db.Sessions
            .Include(s => s.Bidders)
            .Include(s => s.Items)
                .ThenInclude(i => i.Bids)
                    .ThenInclude(b => b.Bidder)
            .Include(s => s.ChatMessages)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper());

        if (session is null || session.AdminToken != adminToken) return null;
        return session;
    }

    // ── Bidder ──────────────────────────────────────────────────────────

    public async Task<Bidder> GetOrCreateBidderAsync(string code, string token)
    {
        var session = await db.Sessions
            .Include(s => s.Bidders)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session ikke fundet");

        var existing = session.Bidders.FirstOrDefault(b => b.Token == token);
        if (existing is not null) return existing;

        var color = Colors[session.Bidders.Count % Colors.Length];
        var bidder = new Bidder { SessionId = session.Id, Token = token, Color = color };
        db.Bidders.Add(bidder);
        await db.SaveChangesAsync();
        return bidder;
    }

    private async Task<int> EnsureBidderNumberAsync(Guid sessionId, Bidder bidder)
    {
        if (bidder.BidderNumber.HasValue) return bidder.BidderNumber.Value;

        var maxNum = await db.Bidders
            .Where(b => b.SessionId == sessionId && b.BidderNumber.HasValue)
            .MaxAsync(b => (int?)b.BidderNumber) ?? 0;

        bidder.BidderNumber = maxNum + 1;
        await db.SaveChangesAsync();
        return bidder.BidderNumber.Value;
    }

    // ── Phase management (admin only) ────────────────────────────────────

    private async Task<AuctionSession> RequireAdminAsync(string code, string adminToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session ikke fundet");
        if (session.AdminToken != adminToken)
            throw new UnauthorizedAccessException("Ugyldig admin-nøgle");
        return session;
    }

    public async Task OpenPreAuctionAsync(string code, string adminToken)
    {
        var session = await RequireAdminAsync(code, adminToken);
        session.Status = AuctionStatus.PreAuction;
        await db.SaveChangesAsync();
        await hub.Clients.Group(code.ToUpper()).SendAsync("PhaseChanged", "PreAuction");
    }

    public async Task StartLiveAsync(string code, string adminToken)
    {
        var session = await RequireAdminAsync(code, adminToken);
        session.Status = AuctionStatus.Live;
        await db.SaveChangesAsync();
        await hub.Clients.Group(code.ToUpper()).SendAsync("PhaseChanged", "Live");
    }

    public async Task EndAuctionAsync(string code, string adminToken)
    {
        var session = await RequireAdminAsync(code, adminToken);
        if (session.ActiveItemId.HasValue)
            countdown.Cancel(session.ActiveItemId.Value);
        session.Status = AuctionStatus.Ended;
        session.ActiveItemId = null;
        await db.SaveChangesAsync();
        await hub.Clients.Group(code.ToUpper()).SendAsync("PhaseChanged", "Ended");
    }

    // ── Items ────────────────────────────────────────────────────────────

    public async Task<AuctionItem> AddItemAsync(
        string code, string adminToken,
        string name, string? description,
        decimal startingPrice, int lotQuantity, string? photoPath)
    {
        var session = await db.Sessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session ikke fundet");

        if (session.AdminToken != adminToken)
            throw new UnauthorizedAccessException("Ugyldig admin-nøgle");

        var item = new AuctionItem
        {
            SessionId     = session.Id,
            Name          = name,
            Description   = description,
            StartingPrice = startingPrice,
            LotQuantity   = lotQuantity,
            PhotoPath     = photoPath,
            SortOrder     = session.Items.Count
        };

        db.Items.Add(item);
        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("ItemAdded", new
        {
            item.Id, item.Name, item.Description,
            item.StartingPrice, item.LotQuantity, item.PhotoPath,
            Status = item.Status.ToString(), item.SortOrder
        });

        return item;
    }

    public async Task ActivateItemAsync(string code, string adminToken, Guid itemId)
    {
        var session = await db.Sessions
            .Include(s => s.Items)
                .ThenInclude(i => i.Bids)
                    .ThenInclude(b => b.Bidder)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session ikke fundet");

        if (session.AdminToken != adminToken)
            throw new UnauthorizedAccessException("Ugyldig admin-nøgle");

        // Deactivate current active item
        if (session.ActiveItemId.HasValue)
            countdown.Cancel(session.ActiveItemId.Value);

        foreach (var active in session.Items.Where(i => i.Status == AuctionItemStatus.Active))
            active.Status = AuctionItemStatus.Pending;

        var item = session.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Genstand ikke fundet");

        // Promote highest pre-bid to live bid
        var topPreBid = item.Bids
            .Where(b => b.IsPreBid)
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault();

        if (topPreBid is not null)
        {
            topPreBid.IsPreBid = false;
            await EnsureBidderNumberAsync(session.Id, topPreBid.Bidder!);
            // Remove lower pre-bids
            var stale = item.Bids.Where(b => b.IsPreBid).ToList();
            db.Bids.RemoveRange(stale);
        }

        item.Status = AuctionItemStatus.Active;
        session.ActiveItemId = itemId;
        await db.SaveChangesAsync();

        countdown.Start(code, itemId, 30);

        await hub.Clients.Group(code.ToUpper()).SendAsync("ItemActivated", new
        {
            item.Id, item.Name, item.Description,
            item.StartingPrice, item.PhotoPath, item.LotQuantity
        });
    }

    // ── Bidding ──────────────────────────────────────────────────────────

    public async Task PlaceBidAsync(string code, Guid itemId, string token, decimal amount)
    {
        var session = await db.Sessions
            .Include(s => s.Bidders)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session ikke fundet");

        var bidder = session.Bidders.FirstOrDefault(b => b.Token == token)
            ?? throw new InvalidOperationException("Budgiver ikke fundet");

        var item = await db.Items
            .Include(i => i.Bids)
            .FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Genstand ikke fundet");

        bool isPreBid = session.Status == AuctionStatus.PreAuction;

        if (!isPreBid && item.Status != AuctionItemStatus.Active)
            throw new InvalidOperationException("Genstand er ikke aktiv");

        var liveBids = item.Bids.Where(b => !b.IsPreBid).ToList();
        decimal minBid = liveBids.Any()
            ? liveBids.Max(b => b.Amount) + 1
            : item.StartingPrice;

        if (!isPreBid && amount < minBid)
            throw new InvalidOperationException($"Mindste bud er {minBid:0} kr");

        if (isPreBid)
        {
            // Replace existing pre-bid from same bidder on same item
            var existing = item.Bids.FirstOrDefault(b => b.IsPreBid && b.BidderId == bidder.Id);
            if (existing is not null) db.Bids.Remove(existing);
        }

        var num = await EnsureBidderNumberAsync(session.Id, bidder);

        var bid = new Bid
        {
            AuctionItemId = itemId,
            BidderId      = bidder.Id,
            Amount        = amount,
            IsPreBid      = isPreBid
        };
        db.Bids.Add(bid);
        await db.SaveChangesAsync();

        if (!isPreBid)
            countdown.Extend(code, itemId, 15);

        await hub.Clients.Group(code.ToUpper()).SendAsync("BidPlaced", new
        {
            bid.Id,
            ItemId       = itemId,
            bid.BidderId,
            BidderNumber = num,
            bid.Amount,
            bid.PlacedAt,
            bid.IsPreBid,
            bidder.Color
        });
    }

    public async Task CloseItemAsync(string code, string adminToken, Guid itemId, bool sold)
    {
        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session ikke fundet");

        if (session.AdminToken != adminToken)
            throw new UnauthorizedAccessException("Ugyldig admin-nøgle");

        countdown.Cancel(itemId);

        var item = await db.Items
            .Include(i => i.Bids)
                .ThenInclude(b => b.Bidder)
            .FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Genstand ikke fundet");

        Bidder? winner = null;

        if (sold)
        {
            var topBid = item.Bids.Where(b => !b.IsPreBid).OrderByDescending(b => b.Amount).FirstOrDefault();
            if (topBid is not null)
            {
                item.WinnerBidderId = topBid.BidderId;
                item.WinningBid     = topBid.Amount;
                item.Status         = AuctionItemStatus.Sold;
                winner              = topBid.Bidder;
            }
            else
            {
                item.Status = AuctionItemStatus.Passed;
            }
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
            ItemId         = itemId,
            Status         = item.Status.ToString(),
            WinnerId       = item.WinnerBidderId,
            WinnerNumber   = winner?.BidderNumber,
            WinnerColor    = winner?.Color,
            WinningBid     = item.WinningBid,
            ItemName       = item.Name
        });
    }

    // ── Buy now ──────────────────────────────────────────────────────────

    public async Task BuyNowAsync(string code, Guid itemId, string buyerName, string buyerPhone)
    {
        var item = await db.Items.FindAsync(itemId)
            ?? throw new InvalidOperationException("Genstand ikke fundet");

        if (item.Status != AuctionItemStatus.Passed)
            throw new InvalidOperationException("Genstand er ikke tilgængelig for køb nu");

        item.BuyNowBuyerName  = buyerName;
        item.BuyNowBuyerPhone = buyerPhone;
        item.Status           = AuctionItemStatus.Sold;
        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("BuyNow", new
        {
            ItemId = itemId, BuyerName = buyerName
        });
    }

    // ── Chat ─────────────────────────────────────────────────────────────

    public async Task SendChatAsync(string code, string token, string message)
    {
        var session = await db.Sessions
            .Include(s => s.Bidders)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Session ikke fundet");

        if (session.Status != AuctionStatus.Ended)
            throw new InvalidOperationException("Chat er kun åben når auktionen er slut");

        var bidder = session.Bidders.FirstOrDefault(b => b.Token == token)
            ?? throw new InvalidOperationException("Budgiver ikke fundet");

        var num = bidder.BidderNumber ?? 0;

        var msg = new ChatMessage
        {
            SessionId    = session.Id,
            BidderNumber = num,
            Message      = message.Trim()
        };
        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("ChatMessage", new
        {
            msg.Id, msg.BidderNumber, msg.Message, msg.SentAt, bidder.Color
        });
    }

    public Task<List<ChatMessage>> GetChatAsync(string code) =>
        db.ChatMessages
            .Join(db.Sessions, c => c.SessionId, s => s.Id, (c, s) => new { c, s.Code })
            .Where(x => x.Code == code.ToUpper())
            .Select(x => x.c)
            .OrderBy(c => c.SentAt)
            .ToListAsync();

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}
