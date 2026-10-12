using ITMartinAuction.Domain;

namespace ITMartinAuction.Application;

public sealed record LagerSession(string Code, string Name, string Status, DateTime? AuctionDate, DateTime CreatedAt, DateTime ExpiresAt, int Items);
public sealed record LagerItemIn(string Code, string Name, string? Description, decimal StartingPrice, int LotQuantity, string? PhotoJpegBase64, string? Highlight = null);
public sealed record LagerStatusOut(Guid Id, string Status, decimal? WinningBid, string? BuyerName, string? BuyerPhone);

/// <summary>Bogshoppen Lager (2026-10-05): it sends items to an auction - now with their guld/sølv/bronze/måske mark
/// (2026-10-12) - and fetches the results back before CleanupJob deletes the session.</summary>
public sealed class LagerUseCases(IAuctionStore store, AuctionUseCases auctions, IPhotoStore photos)
{
    public async Task<List<LagerSession>> OpenSessionsAsync(CancellationToken ct = default) =>
        (await store.OpenSessionsAsync(ct))
            .Select(s => new LagerSession(s.Code, s.Name, s.Status.ToString(), s.AuctionDate, s.CreatedAt, s.ExpiresAt, s.Items.Count))
            .OrderBy(s => s.AuctionDate ?? s.CreatedAt).ToList();

    /// <summary>Null when the auction does not exist; throws when it has ended.</summary>
    public async Task<(Guid Id, string Code)?> AddAsync(LagerItemIn x, CancellationToken ct = default)
    {
        var session = await store.SessionAsync(Codes.Normalise(x.Code), ct);
        if (session is null) return null;
        if (session.Status == AuctionStatus.Ended) throw new InvalidOperationException("Auktionen er slut");
        string? photo = null;
        if (!string.IsNullOrEmpty(x.PhotoJpegBase64))
        {
            using var ms = new MemoryStream(Convert.FromBase64String(x.PhotoJpegBase64));
            photo = await photos.SaveAsync(ms, ".jpg", ct);
        }
        var item = await auctions.AddItemAsync(session.Code, session.AdminToken, x.Name, x.Description, x.StartingPrice,
            Math.Max(1, x.LotQuantity), photo, x.Highlight);
        return (item.Id, session.Code);
    }

    public async Task<List<LagerStatusOut>> StatusAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var items = await store.ItemsAsync(ids, ct);
        var bidders = (await store.BiddersAsync(items.Where(i => i.WinnerBidderId is not null).Select(i => i.WinnerBidderId!.Value).ToList(), ct))
            .ToDictionary(b => b.Id);
        return ids.Select(id =>
        {
            var i = items.FirstOrDefault(x => x.Id == id);
            if (i is null) return new LagerStatusOut(id, "Gone", null, null, null);
            var w = i.WinnerBidderId is { } wid ? bidders.GetValueOrDefault(wid) : null;
            return new LagerStatusOut(id, i.Status.ToString(), i.WinningBid, w?.Name ?? i.BuyNowBuyerName, w?.Phone ?? i.BuyNowBuyerPhone);
        }).ToList();
    }
}
