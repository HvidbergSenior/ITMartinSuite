using ITMartinLager.Domain;

namespace ITMartinLager.Application;

// ShopItem / ShopProof / ShopMarket / ShopQuote are the shapes bogshoppen.dk gets (ITMartinVarer.Contracts).

/// <summary>The shop on bogshoppen.dk: the marketplace by product type, the price promise with its proof links, today's
/// offers, and Køb nu for one thing or a basket through Stripe. The rules (who is listed, what a basket costs today,
/// what a sale may do next) are in the Domain; this only fetches, asks them and saves.</summary>
public sealed class ShopUseCases(IItemRepository items, IPriceRepository prices, IOrderRepository orders, IPaymentGateway payments,
    ICampaignRepository campaigns, IGoldRepository gold, LagerOptions options, TimeProvider clock)
{
    public const int MaxResults = 60;
    /// <summary>A basket is at most this many things (a bundle is 10 - room for a few bundles).</summary>
    public const int MaxBasket = 50;

    /// <summary>Stripe keys are in, and there is a way to get the thing (shipping or pickup).</summary>
    public bool CanPay => payments.Enabled && (options.ShopShippingKr > 0 || options.ShopPickup != "");

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public async Task<List<ShopItem>> SearchAsync(string? text, string? kind, CancellationToken ct = default) =>
        (await MarketAsync(null, kind, text, ShopSorts.Title, 0, ct)).Items;

    /// <summary>The marketplace (user 2026-10-20: "a place where they easily can see products in different categories"):
    /// the product types with how much is for sale in each, today's offers, and one page of things - of one type, one
    /// kind or everything, matching the search.</summary>
    public async Task<ShopMarket> MarketAsync(string? type, string? kind, string? text, string? sort, int page, CancellationToken ct = default)
    {
        IReadOnlyCollection<string>? kinds = !string.IsNullOrWhiteSpace(kind) ? [Kinds.Normalise(kind)]
            : !string.IsNullOrWhiteSpace(type) && ProductTypes.All.Contains(type) ? ProductTypes.KindsOf(type) : null;
        var (found, total) = await items.ListedAsync(kinds, (text ?? "").Trim(), ShopSorts.All.Contains(sort) ? sort! : ShopSorts.Newest,
            Math.Max(0, page) * MaxResults, MaxResults, ct);
        var perKind = await items.ListedPerKindAsync(ct);
        var types = ProductTypes.All.Select(t => new ShopType(t, ProductTypes.KindsOf(t).Sum(k => perKind.GetValueOrDefault(k))))
            .Where(t => t.Count > 0).ToList();
        return new ShopMarket(CanPay, types, await TodayAsync(ct), await ViewsAsync(found, ct), total);
    }

    /// <summary>Today's offers, as the buyer reads them.</summary>
    public async Task<List<ShopOffer>> TodayAsync(CancellationToken ct = default) =>
        (await campaigns.AllAsync(ct)).Where(c => c.On(Today)).Select(c => new ShopOffer(c.Name, c.Text, c.When)).ToList();

    public async Task<(ShopItem Item, List<ShopProof> Proof)?> ItemAsync(int id, CancellationToken ct = default)
    {
        if (await items.GetAsync(id, ct) is not { } item || !Shop.Listed(item)) return null;
        var (finds, _) = await prices.ForItemAsync(id, ct);
        return ((await ViewsAsync([item], ct))[0],
            Shop.Proof(finds).Select(f => new ShopProof(f.Source, f.Kr, f.Original, f.Url, f.IsNew)).ToList());
    }

    /// <summary>"Fundet i kasserne" on both front pages: the chosen items, each with its proof links ("kan også købes her").</summary>
    public async Task<List<(ShopItem Item, List<ShopProof> Proof)>> FeaturedAsync(int max = 12, CancellationToken ct = default)
    {
        var chosen = (await items.FeaturedAsync(max, ct)).Where(Shop.Listed).ToList();
        var views = await ViewsAsync(chosen, ct);
        var list = new List<(ShopItem, List<ShopProof>)>();
        for (var n = 0; n < chosen.Count; n++)
        {
            var (finds, _) = await prices.ForItemAsync(chosen[n].Id, ct);
            list.Add((views[n], Shop.Proof(finds, 4).Select(f => new ShopProof(f.Source, f.Kr, f.Original, f.Url, f.IsNew)).ToList()));
        }
        return list;
    }

    public async Task<string?> PhotoPathAsync(int id, CancellationToken ct = default) =>
        await items.GetAsync(id, ct) is { ItemPhoto.Length: > 0 } i && Shop.Listed(i) ? i.ItemPhoto : null;

    /// <summary>The basket priced for today: what each thing costs with today's offers, and the ones that are gone
    /// (sold, reserved, no price) - the basket page shows them so the buyer knows.</summary>
    public async Task<ShopQuote> QuoteAsync(IReadOnlyList<int> itemIds, CancellationToken ct = default)
    {
        var (lines, gone) = await BasketAsync(itemIds, ct);
        var priced = Campaigns.Price(lines.Select(l => new BasketLine(l.Item.Id, l.Item.Kind, l.Kr)).ToList(), await campaigns.AllAsync(ct), Today);
        return new ShopQuote(CanPay,
            priced.Lines.Select((p, n) => new ShopQuoteLine(p.ItemId, lines[n].Item.Line(), p.ListKr, p.Kr, p.Offer)).ToList(),
            priced.Total, priced.Saved, await TodayAsync(ct), gone.Select(g => g.Id).Distinct().ToList());
    }

    /// <summary>Køb nu for one thing.</summary>
    public Task<string> BuyAsync(int id, string siteBase, CancellationToken ct = default) => BuyAsync([id], siteBase, ct);

    /// <summary>Køb nu for a basket: today's offers applied, every copy reserved, and Stripe's payment page returned.
    /// Refused when something in it is gone - the buyer sees the basket again first.</summary>
    public async Task<string> BuyAsync(IReadOnlyList<int> itemIds, string siteBase, CancellationToken ct = default)
    {
        if (!CanPay) throw new LagerException("Betaling er ikke slået til endnu. Skriv til os, så finder vi ud af det.");
        var (lines, gone) = await BasketAsync(itemIds, ct);
        if (lines.Count == 0 && gone.Count == 0) throw new LagerException("Kurven er tom.");
        if (gone.Count > 0) throw new LagerException(itemIds.Count == 1 ? gone[0].Why : "Noget i kurven er lige blevet solgt eller reserveret. Se kurven igen.");
        var priced = Campaigns.Price(lines.Select(l => new BasketLine(l.Item.Id, l.Item.Kind, l.Kr)).ToList(), await campaigns.AllAsync(ct), Today);
        var order = Order.For(lines.Select((l, n) => (l.Item, priced.Lines[n])).ToList(), Now);
        await orders.AddAsync(order, ct);
        var site = siteBase.TrimEnd('/');
        var back = itemIds.Count == 1 ? $"{site}/varer/{itemIds[0]}" : $"{site}/kurv";
        var (checkoutId, url) = await payments.StartAsync(order, $"{order.Lines.Count} ting fra Bogshoppen",
            $"{site}/varer/koebt?ordre={order.Token}", back, options.ShopShippingKr, options.ShopPickup, ct);
        order.CheckoutId = checkoutId;
        await orders.UpdateAsync(order, ct);
        return url;
    }

    /// <summary>The buyer is back from Stripe: paid -> the copies leave the stock.</summary>
    public async Task<Order?> ConfirmAsync(string token, CancellationToken ct = default)
    {
        if (await orders.ByTokenAsync(token, ct) is not { } order) return null;
        await CheckAsync(order, ct);
        return order;
    }

    /// <summary>Every pending order: paid ones are booked, expired ones free their copies. Returns how many changed.</summary>
    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        var changed = 0;
        foreach (var o in await orders.PendingAsync(ct))
            if (await CheckAsync(o, ct)) changed++;
        return changed;
    }

    public Task<List<Order>> RecentOrdersAsync(CancellationToken ct = default) => orders.RecentAsync(200, ct);

    // ---- after payment: Pakket -> Sendt / Afhentet (Lager's Ordrer page) ----

    public Task PackAsync(int orderId, CancellationToken ct = default) => StepAsync(orderId, o => o.Pack(Now), ct);
    public Task SendAsync(int orderId, string tracking, CancellationToken ct = default) => StepAsync(orderId, o => o.Send(tracking, Now), ct);
    public Task PickedUpAsync(int orderId, CancellationToken ct = default) => StepAsync(orderId, o => o.PickUp(Now), ct);
    public Task UndoAsync(int orderId, CancellationToken ct = default) => StepAsync(orderId, o => o.Undo(), ct);

    private async Task StepAsync(int orderId, Action<Order> step, CancellationToken ct)
    {
        var order = await orders.GetAsync(orderId, ct) ?? throw new LagerException("Ordren findes ikke.");
        try { step(order); }
        catch (InvalidOperationException ex) { throw new LagerException(ex.Message); }
        await orders.UpdateAsync(order, ct);
    }

    private async Task<bool> CheckAsync(Order order, CancellationToken ct)
    {
        if (order.Status != OrderStatuses.Pending) return false;
        var state = order.CheckoutId == "" ? new PaymentState(false, order.ExpiresAt < Now) : await payments.StateAsync(order.CheckoutId, ct);
        if (state.Paid)
        {
            // An item deleted meanwhile is simply not there - the order still says it was paid.
            var sold = (await items.ByIdsAsync(order.Lines.Select(l => l.ItemId).Distinct().ToList(), ct)).ToDictionary(i => i.Id);
            order.Pay(sold, state, Now);
            await items.UpdateAsync(sold.Values, ct);
        }
        else if (state.Expired) order.Expire();
        else return false;
        await orders.UpdateAsync(order, ct);
        return true;
    }

    // The basket's things with their Køb nu price; a copy is gone when it is sold, reserved by someone paying, has no
    // price, or the basket asks for more copies than are free.
    private async Task<(List<(Item Item, int Kr)> Lines, List<(int Id, string Why)> Gone)> BasketAsync(IReadOnlyList<int> itemIds, CancellationToken ct)
    {
        var ids = itemIds.Take(MaxBasket).ToList();
        var found = (await items.ByIdsAsync(ids.Distinct().ToList(), ct)).ToDictionary(i => i.Id);
        var reserved = await orders.ReservedAsync(found.Keys.ToList(), Now, ct);
        var taken = new Dictionary<int, int>();
        var lines = new List<(Item, int)>();
        var gone = new List<(int, string)>();
        foreach (var id in ids)
        {
            string? why = null;
            int kr = 0;
            if (found.GetValueOrDefault(id) is not { } item || !Shop.Listed(item)) why = "Varen er desværre solgt.";
            else if (Shop.BuyNowKr(item, options.ShopAutoMaxKr) is not { } price) why = "Varen har ikke en fast pris endnu.";
            else if (item.Quantity - reserved.GetValueOrDefault(id) - taken.GetValueOrDefault(id) <= 0)
                why = "En anden er ved at betale for den lige nu. Prøv igen om en halv time.";
            else kr = price;
            if (why is not null) { gone.Add((id, why)); continue; }
            taken[id] = taken.GetValueOrDefault(id) + 1;
            lines.Add((found[id], kr));
        }
        return (lines, gone);
    }

    private async Task<List<ShopItem>> ViewsAsync(IReadOnlyList<Item> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var reserved = await orders.ReservedAsync(list.Select(i => i.Id).ToList(), Now, ct);
        var offers = await campaigns.AllAsync(ct);
        var goldList = await gold.AllAsync(ct);
        return list.Select(i =>
        {
            var buyNow = Shop.BuyNowKr(i, options.ShopAutoMaxKr);
            var day = buyNow is { } kr ? Campaigns.DayPrice(i.Kind, kr, offers, Today) : null;
            var dayOffer = day is null ? "" : offers.Where(c => c.On(Today) && c.Kind == CampaignKinds.Percent && c.Fits(i.Kind)).MaxBy(c => c.Percent)!.Text;
            return new ShopItem(i.Id, i.Kind, i.Line(), i.Title, i.Interest, i.Condition, i.Year, buyNow,
                i.PriceHint is > 0 ? (int)Math.Round(i.PriceHint.Value) : null,
                Math.Max(0, i.Quantity - reserved.GetValueOrDefault(i.Id)), i.ItemPhoto != "",
                ProductTypes.For(i.Kind), day, dayOffer, Shop.Badge(goldList, i));
        }).ToList();
    }
}
