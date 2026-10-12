using ITMartinAuction.Domain;

namespace ITMartinAuction.Application;

/// <summary>Running an auction: create, phases, items, bidding, hammer, buy now, chat. (The old AuctionService, moved
/// into use cases with ports on 2026-10-12 - same behaviour, same messages to the phones.)</summary>
public sealed class AuctionUseCases(IAuctionStore store, IAuctionBroadcast hub, ICountdown countdown, IPhotoStore photos)
{
    // ── Sessions ───────────────────────────────────────────────────────

    public async Task<AuctionSession> CreateAsync(string name)
    {
        var session = new AuctionSession { Name = name, Code = Codes.Generate(Random.Shared) };
        store.Add(session);
        await store.SaveAsync();
        return session;
    }

    public Task<AuctionSession?> GetByCodeAsync(string code) => store.SessionFullAsync(Codes.Normalise(code));

    public async Task<AuctionSession?> GetAdminSessionAsync(string code, string adminToken)
    {
        var session = await store.SessionFullAsync(Codes.Normalise(code), withChat: true);
        return session is null || session.AdminToken != adminToken ? null : session;
    }

    // ── Bidders ────────────────────────────────────────────────────────

    public async Task<Bidder> GetOrCreateBidderAsync(string code, string token)
    {
        var session = await store.SessionWithBiddersAsync(Codes.Normalise(code)) ?? throw new InvalidOperationException("Session ikke fundet");
        if (session.Bidders.FirstOrDefault(b => b.Token == token) is { } existing) return existing;
        var bidder = new Bidder { SessionId = session.Id, Token = token, Color = BidderColors.For(session.Bidders.Count) };
        store.Add(bidder);
        await store.SaveAsync();
        return bidder;
    }

    private async Task<int> EnsureBidderNumberAsync(Guid sessionId, Bidder bidder)
    {
        if (bidder.BidderNumber is { } n) return n;
        bidder.BidderNumber = await store.MaxBidderNumberAsync(sessionId) + 1;
        await store.SaveAsync();
        return bidder.BidderNumber.Value;
    }

    // ── Phases (admin only) ────────────────────────────────────────────

    private async Task<AuctionSession> RequireAdminAsync(string code, string adminToken, Func<string, Task<AuctionSession?>>? load = null)
    {
        var session = await (load ?? (c => store.SessionAsync(c)))(Codes.Normalise(code)) ?? throw new InvalidOperationException("Session ikke fundet");
        if (session.AdminToken != adminToken) throw new UnauthorizedAccessException("Ugyldig admin-nøgle");
        return session;
    }

    public Task OpenPreAuctionAsync(string code, string adminToken) => SetPhaseAsync(code, adminToken, AuctionStatus.PreAuction, "PreAuction");
    public Task StartLiveAsync(string code, string adminToken) => SetPhaseAsync(code, adminToken, AuctionStatus.Live, "Live");

    private async Task SetPhaseAsync(string code, string adminToken, AuctionStatus status, string name)
    {
        var session = await RequireAdminAsync(code, adminToken);
        session.Status = status;
        await store.SaveAsync();
        await hub.SendAsync(session.Code, "PhaseChanged", name);
    }

    public async Task EndAuctionAsync(string code, string adminToken)
    {
        var session = await RequireAdminAsync(code, adminToken);
        if (session.ActiveItemId is { } active) countdown.Cancel(active);
        session.Status = AuctionStatus.Ended;
        session.ActiveItemId = null;
        await store.SaveAsync();
        await hub.SendAsync(session.Code, "PhaseChanged", "Ended");
    }

    // ── Items ──────────────────────────────────────────────────────────

    public Task<string> SavePhotoAsync(Stream photo, string extension) => photos.SaveAsync(photo, extension);
    public string PhotoUrl(string fullPath) => photos.PublicUrl(fullPath);

    public async Task<AuctionItem> AddItemAsync(string code, string adminToken, string name, string? description,
        decimal startingPrice, int lotQuantity, string? photoPath, string? highlight = null)
    {
        var session = await RequireAdminAsync(code, adminToken, c => store.SessionWithItemsAsync(c));
        var item = new AuctionItem
        {
            SessionId = session.Id, Name = name, Description = description, StartingPrice = startingPrice,
            LotQuantity = lotQuantity, PhotoPath = photoPath, SortOrder = session.Items.Count,
            Highlight = string.IsNullOrWhiteSpace(highlight) ? null : highlight.Trim(),
        };
        store.Add(item);
        await store.SaveAsync();
        await hub.SendAsync(session.Code, "ItemAdded", new
        {
            item.Id, item.Name, item.Description, item.StartingPrice, item.LotQuantity, item.PhotoPath,
            Status = item.Status.ToString(), item.SortOrder, item.Highlight,
        });
        return item;
    }

    public async Task ActivateItemAsync(string code, string adminToken, Guid itemId)
    {
        var session = await RequireAdminAsync(code, adminToken, c => store.SessionFullAsync(c));
        if (session.ActiveItemId is { } current) countdown.Cancel(current);
        foreach (var active in session.Items.Where(i => i.Status == AuctionItemStatus.Active)) active.Status = AuctionItemStatus.Pending;

        var item = session.Items.FirstOrDefault(i => i.Id == itemId) ?? throw new InvalidOperationException("Genstand ikke fundet");
        var (promoted, stale) = Bidding.PromotePreBid(item);
        if (promoted is not null)
        {
            await EnsureBidderNumberAsync(session.Id, promoted.Bidder!);
            store.Remove(stale);
        }
        item.Status = AuctionItemStatus.Active;
        session.ActiveItemId = itemId;
        await store.SaveAsync();

        countdown.Start(session.Code, itemId, Bidding.FirstCountdownSeconds);
        await hub.SendAsync(session.Code, "ItemActivated", new
        {
            item.Id, item.Name, item.Description, item.StartingPrice, item.PhotoPath, item.LotQuantity, item.Highlight,
        });
    }

    // ── Bidding ────────────────────────────────────────────────────────

    public async Task PlaceBidAsync(string code, Guid itemId, string token, decimal amount)
    {
        var session = await store.SessionWithBiddersAsync(Codes.Normalise(code)) ?? throw new InvalidOperationException("Session ikke fundet");
        var bidder = session.Bidders.FirstOrDefault(b => b.Token == token) ?? throw new InvalidOperationException("Budgiver ikke fundet");
        var item = await store.ItemAsync(itemId, withBids: true) ?? throw new InvalidOperationException("Genstand ikke fundet");

        var isPreBid = session.Status == AuctionStatus.PreAuction;
        if (!isPreBid && item.Status != AuctionItemStatus.Active) throw new InvalidOperationException("Genstand er ikke aktiv");
        var minBid = Bidding.MinBid(item);
        if (!isPreBid && amount < minBid) throw new InvalidOperationException($"Mindste bud er {minBid:0} kr");
        if (isPreBid && item.Bids.FirstOrDefault(b => b.IsPreBid && b.BidderId == bidder.Id) is { } replaced) store.Remove([replaced]);

        var num = await EnsureBidderNumberAsync(session.Id, bidder);
        var bid = new Bid { AuctionItemId = itemId, BidderId = bidder.Id, Amount = amount, IsPreBid = isPreBid };
        store.Add(bid);
        await store.SaveAsync();

        if (!isPreBid) countdown.Extend(session.Code, itemId, Bidding.ExtendSeconds);
        await hub.SendAsync(session.Code, "BidPlaced", new
        {
            bid.Id, ItemId = itemId, bid.BidderId, BidderNumber = num, bid.Amount, bid.PlacedAt, bid.IsPreBid, bidder.Color,
        });
    }

    public async Task CloseItemAsync(string code, string adminToken, Guid itemId, bool sold)
    {
        var session = await RequireAdminAsync(code, adminToken);
        countdown.Cancel(itemId);
        var item = await store.ItemAsync(itemId, withBids: true, withBidders: true) ?? throw new InvalidOperationException("Genstand ikke fundet");
        var winner = Bidding.Close(item, sold)?.Bidder;
        if (session.ActiveItemId == itemId) session.ActiveItemId = null;
        await store.SaveAsync();
        await hub.SendAsync(session.Code, "ItemClosed", new
        {
            ItemId = itemId, Status = item.Status.ToString(), WinnerId = item.WinnerBidderId, WinnerNumber = winner?.BidderNumber,
            WinnerColor = winner?.Color, item.WinningBid, ItemName = item.Name,
        });
    }

    public async Task BuyNowAsync(string code, Guid itemId, string buyerName, string buyerPhone)
    {
        var item = await store.ItemAsync(itemId) ?? throw new InvalidOperationException("Genstand ikke fundet");
        Bidding.BuyNow(item, buyerName, buyerPhone);
        await store.SaveAsync();
        await hub.SendAsync(Codes.Normalise(code), "BuyNow", new { ItemId = itemId, BuyerName = buyerName });
    }

    // ── Chat ───────────────────────────────────────────────────────────

    public async Task SendChatAsync(string code, string token, string message)
    {
        var session = await store.SessionWithBiddersAsync(Codes.Normalise(code)) ?? throw new InvalidOperationException("Session ikke fundet");
        if (session.Status != AuctionStatus.Ended) throw new InvalidOperationException("Chat er kun åben når auktionen er slut");
        var bidder = session.Bidders.FirstOrDefault(b => b.Token == token) ?? throw new InvalidOperationException("Budgiver ikke fundet");
        var msg = new ChatMessage { SessionId = session.Id, BidderNumber = bidder.BidderNumber ?? 0, Message = message.Trim() };
        store.Add(msg);
        await store.SaveAsync();
        await hub.SendAsync(session.Code, "ChatMessage", new { msg.Id, msg.BidderNumber, msg.Message, msg.SentAt, bidder.Color });
    }

    public Task<List<ChatMessage>> GetChatAsync(string code) => store.ChatAsync(Codes.Normalise(code));
}

/// <summary>Sessions are deleted 7 days after they were created (Bogshoppen Lager fetches results long before).</summary>
public sealed class CleanupUseCases(IAuctionStore store, TimeProvider clock)
{
    public async Task<int> RemoveExpiredAsync(CancellationToken ct = default)
    {
        var expired = await store.ExpiredSessionsAsync(clock.GetUtcNow().UtcDateTime, ct);
        store.Remove(expired);
        await store.SaveAsync(ct);
        return expired.Count;
    }
}
