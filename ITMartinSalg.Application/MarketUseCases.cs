using ITMartinSalg.Domain;

namespace ITMartinSalg.Application;

/// <summary>Sales at the loppemarked without finding the thing (user 2026-10-31: "just take a picture on the product in salg"
/// -> "yes to free"): the photo and the price are saved as a done sale. No AI - the photo is only kept to look at.</summary>
public sealed class MarketUseCases(IOrderRepository orders, IMarketPhotos photos, TimeProvider clock)
{
    /// <summary>A phone photo is a few MB; more than this is not a photo.</summary>
    public const long MaxPhotoBytes = 15 * 1024 * 1024;

    public async Task<Order> SellFreeAsync(string what, int kr, Stream? photo, CancellationToken ct = default)
    {
        Order order;
        try { order = Order.FreeAtMarket(what, kr, clock.GetUtcNow().UtcDateTime); }
        catch (InvalidOperationException ex) { throw new SalgException(ex.Message); }
        await orders.AddAsync(order, ct);
        if (photo is not null) await photos.SaveAsync(order.Id, photo, ct);
        return order;
    }

    public Task<byte[]?> PhotoAsync(int orderId, CancellationToken ct = default) => photos.GetAsync(orderId, ct);
}
