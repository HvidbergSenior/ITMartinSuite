using ITMartinAuction.Domain;

namespace ITMartinAuction.Application;

// What the use cases need from the outside world. Implemented in ITMartinAuction.Infrastructure (storage, photos) and
// in the Server (SignalR broadcast, countdown).

/// <summary>The auction store, one unit of work per scope: what is loaded is tracked, SaveAsync writes it all.</summary>
public interface IAuctionStore
{
    void Add(AuctionSession session);
    void Add(AuctionItem item);
    void Add(Bidder bidder);
    void Add(Bid bid);
    void Add(ChatMessage message);
    void Remove(IEnumerable<Bid> bids);
    void Remove(IEnumerable<AuctionSession> sessions);

    Task<AuctionSession?> SessionAsync(string code, CancellationToken ct = default);
    Task<AuctionSession?> SessionWithBiddersAsync(string code, CancellationToken ct = default);
    Task<AuctionSession?> SessionWithItemsAsync(string code, CancellationToken ct = default);
    /// <summary>Bidders + items + their bids + each bid's bidder (+ chat when asked).</summary>
    Task<AuctionSession?> SessionFullAsync(string code, bool withChat = false, CancellationToken ct = default);
    Task<AuctionItem?> ItemAsync(Guid id, bool withBids = false, bool withBidders = false, CancellationToken ct = default);
    Task<List<AuctionItem>> ItemsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<List<Bidder>> BiddersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<int> MaxBidderNumberAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<ChatMessage>> ChatAsync(string code, CancellationToken ct = default);
    Task<List<AuctionSession>> OpenSessionsAsync(CancellationToken ct = default);
    Task<List<AuctionSession>> ExpiredSessionsAsync(DateTime now, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

/// <summary>Tells everyone in an auction what happened (SignalR group per code).</summary>
public interface IAuctionBroadcast
{
    Task SendAsync(string code, string message, object? payload = null);
}

/// <summary>The hammer countdown per active item.</summary>
public interface ICountdown
{
    void Start(string code, Guid itemId, int seconds);
    void Extend(string code, Guid itemId, int seconds);
    void Cancel(Guid itemId);
}

/// <summary>Item photos. Returns the full file path, stored on the item.</summary>
public interface IPhotoStore
{
    Task<string> SaveAsync(Stream photo, string extension, CancellationToken ct = default);
    /// <summary>"/photos/abc.jpg" for a stored path.</summary>
    string PublicUrl(string fullPath);
}
