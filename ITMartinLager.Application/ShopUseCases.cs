using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>One thing in the shop, as bogshoppen.dk shows it. BuyNowKr null = no Køb nu yet (ExpectKr = what we think it
/// is worth). Available = copies not reserved by someone paying right now.</summary>
public sealed record ShopItem(int Id, string Kind, string Line, string Title, string Interest, string Condition, int? Year,
    int? BuyNowKr, int? ExpectKr, int Available, bool HasPhoto);

public sealed record ShopProof(string Source, int Kr, string Original, string Url, bool IsNew);

/// <summary>The shop on bogshoppen.dk: search, the price promise with its proof links, and Køb nu through Stripe.</summary>
public sealed class ShopUseCases(IItemRepository items, IPriceRepository prices, IOrderRepository orders, IPaymentGateway payments,
    LagerOptions options, TimeProvider clock)
{
    public const int MaxResults = 60;

    /// <summary>Stripe keys are in, and there is a way to get the thing (shipping or pickup).</summary>
    public bool CanPay => payments.Enabled && (options.ShopShippingKr > 0 || options.ShopPickup != "");

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<List<ShopItem>> SearchAsync(string? text, string? kind, CancellationToken ct = default)
    {
        var found = (await items.SearchAsync((text ?? "").Trim(), string.IsNullOrWhiteSpace(kind) ? null : Kinds.Normalise(kind), null, 400, ct))
            .Where(Shop.Listed).Take(MaxResults).ToList();
        var reserved = await orders.ReservedAsync(found.Select(i => i.Id).ToList(), Now, ct);
        return found.Select(i => View(i, reserved.GetValueOrDefault(i.Id))).ToList();
    }

    public async Task<(ShopItem Item, List<ShopProof> Proof)?> ItemAsync(int id, CancellationToken ct = default)
    {
        if (await items.GetAsync(id, ct) is not { } item || !Shop.Listed(item)) return null;
        var reserved = await orders.ReservedAsync([id], Now, ct);
        var (finds, _) = await prices.ForItemAsync(id, ct);
        return (View(item, reserved.GetValueOrDefault(id)),
            Shop.Proof(finds).Select(f => new ShopProof(f.Source, f.Kr, f.Original, f.Url, f.IsNew)).ToList());
    }

    /// <summary>"Fundet i kasserne" on both front pages: the chosen items, each with its proof links ("kan også købes her").</summary>
    public async Task<List<(ShopItem Item, List<ShopProof> Proof)>> FeaturedAsync(int max = 12, CancellationToken ct = default)
    {
        var chosen = (await items.FeaturedAsync(max, ct)).Where(Shop.Listed).ToList();
        var reserved = await orders.ReservedAsync(chosen.Select(i => i.Id).ToList(), Now, ct);
        var list = new List<(ShopItem, List<ShopProof>)>();
        foreach (var i in chosen)
        {
            var (finds, _) = await prices.ForItemAsync(i.Id, ct);
            list.Add((View(i, reserved.GetValueOrDefault(i.Id)), Shop.Proof(finds, 4).Select(f => new ShopProof(f.Source, f.Kr, f.Original, f.Url, f.IsNew)).ToList()));
        }
        return list;
    }

    public async Task<string?> PhotoPathAsync(int id, CancellationToken ct = default) =>
        await items.GetAsync(id, ct) is { ItemPhoto.Length: > 0 } i && Shop.Listed(i) ? i.ItemPhoto : null;

    /// <summary>Køb nu: reserves one copy and returns Stripe's payment page. siteBase = where the buyer comes back to.</summary>
    public async Task<string> BuyAsync(int id, string siteBase, CancellationToken ct = default)
    {
        if (!CanPay) throw new LagerException("Betaling er ikke slået til endnu. Skriv til os, så finder vi ud af det.");
        var item = await items.GetAsync(id, ct);
        if (item is null || !Shop.Listed(item)) throw new LagerException("Varen er desværre solgt.");
        if (Shop.BuyNowKr(item, options.ShopAutoMaxKr) is not { } kr) throw new LagerException("Varen har ikke en fast pris endnu.");
        var reserved = (await orders.ReservedAsync([id], Now, ct)).GetValueOrDefault(id);
        if (item.Quantity - reserved <= 0) throw new LagerException("En anden er ved at betale for den lige nu. Prøv igen om en halv time.");

        var order = new Order { ItemId = id, Line = item.Line(), Kr = kr, CreatedAt = Now, ExpiresAt = Now + Order.PayWithin + TimeSpan.FromMinutes(1) };
        await orders.AddAsync(order, ct);
        var site = siteBase.TrimEnd('/');
        var (checkoutId, url) = await payments.StartAsync(order, $"{item.Kind} · {item.Condition} stand",
            $"{site}/varer/koebt?ordre={order.Token}", $"{site}/varer/{id}", options.ShopShippingKr, options.ShopPickup, ct);
        order.CheckoutId = checkoutId;
        await orders.UpdateAsync(order, ct);
        return url;
    }

    /// <summary>The buyer is back from Stripe: paid -> the copy leaves the stock.</summary>
    public async Task<Order?> ConfirmAsync(string token, CancellationToken ct = default)
    {
        if (await orders.ByTokenAsync(token, ct) is not { } order) return null;
        await CheckAsync(order, ct);
        return order;
    }

    /// <summary>Every pending order: paid ones are booked, expired ones free their copy. Returns how many changed.</summary>
    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        var changed = 0;
        foreach (var o in await orders.PendingAsync(ct))
            if (await CheckAsync(o, ct)) changed++;
        return changed;
    }

    public Task<List<Order>> RecentOrdersAsync(CancellationToken ct = default) => orders.RecentAsync(100, ct);

    public Task SetHandledAsync(int id, bool handled, CancellationToken ct = default) => orders.SetHandledAsync(id, handled, ct);

    private async Task<bool> CheckAsync(Order order, CancellationToken ct)
    {
        if (order.Status != OrderStatuses.Pending) return false;
        var state = order.CheckoutId == "" ? new PaymentState(false, order.ExpiresAt < Now) : await payments.StateAsync(order.CheckoutId, ct);
        if (state.Paid)
        {
            var item = await items.GetAsync(order.ItemId, ct);
            if (item is not null)
            {
                order.Pay(item, state, Now);
                await items.UpdateAsync([item], ct);
            }
            else order.Status = OrderStatuses.Paid;   // deleted meanwhile - the order still says it was paid
        }
        else if (state.Expired) order.Status = OrderStatuses.Expired;
        else return false;
        await orders.UpdateAsync(order, ct);
        return true;
    }

    private ShopItem View(Item i, int reserved) => new(i.Id, i.Kind, i.Line(), i.Title, i.Interest, i.Condition, i.Year,
        Shop.BuyNowKr(i, options.ShopAutoMaxKr), i.PriceHint is > 0 ? (int)Math.Round(i.PriceHint.Value) : null,
        Math.Max(0, i.Quantity - reserved), i.ItemPhoto != "");
}
