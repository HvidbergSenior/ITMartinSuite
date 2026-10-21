using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>What Salg asks Lager (2026-10-21 split, /api/salg/*): the things for sale with their proof and photo, and a
/// paid sale to take out of the stock. Who is listed and what the proof is are rules in the Domain (Shop); the price a
/// buyer pays, the offers and the orders are Salg's.</summary>
public sealed class StockForSaleUseCases(IItemRepository items, IPriceRepository prices, IGoldRepository gold, ISaleBookingRepository bookings,
    TimeProvider clock)
{
    public const int MaxPage = 100;

    public async Task<StockPage> ListedAsync(IReadOnlyCollection<string>? kinds, string? usage, string? text, string? sort, int skip, int take,
        CancellationToken ct = default)
    {
        var (found, total) = await items.ListedAsync(kinds is { Count: > 0 } ? kinds.Select(Kinds.Normalise).ToList() : null,
            string.IsNullOrWhiteSpace(usage) ? null : Usages.Normalise(usage), (text ?? "").Trim(),
            ShopSorts.All.Contains(sort) ? sort! : ShopSorts.Newest, Math.Max(0, skip), Math.Clamp(take, 1, MaxPage), ct);
        return new StockPage(await ViewsAsync(found, ct), total);
    }

    public Task<Dictionary<string, int>> PerKindAsync(CancellationToken ct = default) => items.ListedPerKindAsync(ct);

    public async Task<StockItemPage?> ItemAsync(int id, CancellationToken ct = default)
    {
        if (await items.GetAsync(id, ct) is not { } item || !Shop.Listed(item)) return null;
        var (finds, _) = await prices.ForItemAsync(id, ct);
        return new StockItemPage((await ViewsAsync([item], ct))[0], Shop.Proof(finds).Select(Shop.ProofView).ToList());
    }

    /// <summary>The ones of these that are for sale (a basket).</summary>
    public async Task<List<StockItem>> ByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        await ViewsAsync((await items.ByIdsAsync(ids.Distinct().Take(MaxPage).ToList(), ct)).Where(Shop.Listed).ToList(), ct);

    /// <summary>"Fundet i kasserne": the chosen items, each with its proof links ("kan også købes her").</summary>
    public async Task<List<StockItemPage>> FeaturedAsync(int max, CancellationToken ct = default)
    {
        var chosen = (await items.FeaturedAsync(Math.Clamp(max, 1, 50), ct)).Where(Shop.Listed).ToList();
        var views = await ViewsAsync(chosen, ct);
        var list = new List<StockItemPage>();
        for (var n = 0; n < chosen.Count; n++)
        {
            var (finds, _) = await prices.ForItemAsync(chosen[n].Id, ct);
            list.Add(new StockItemPage(views[n], Shop.Proof(finds, 4).Select(Shop.ProofView).ToList()));
        }
        return list;
    }

    /// <summary>The item's own photo, only while it is for sale.</summary>
    public async Task<string?> PhotoPathAsync(int id, CancellationToken ct = default) =>
        await items.GetAsync(id, ct) is { ItemPhoto.Length: > 0 } i && Shop.Listed(i) ? i.ItemPhoto : null;

    /// <summary>A paid sale from Salg: one copy per line leaves the stock (the last one = Solgt). A Ref already booked
    /// changes nothing, so Salg may send it again when it did not get the answer. An item deleted meanwhile is skipped.</summary>
    public async Task<bool> BookAsync(StockSale sale, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sale.Ref)) throw new LagerException("Salget har ingen reference.");
        if (await bookings.ExistsAsync(sale.Ref, ct)) return false;
        var now = clock.GetUtcNow().UtcDateTime;
        var sold = (await items.ByIdsAsync(sale.Lines.Select(l => l.ItemId).Distinct().ToList(), ct)).ToDictionary(i => i.Id);
        foreach (var l in sale.Lines)
            if (sold.GetValueOrDefault(l.ItemId) is { } item)
                item.SellCopy(l.Kr, sale.Buyer, now, l.How);
        await bookings.BookAsync(sale.Ref, sold.Values, ct);
        return true;
    }

    private async Task<List<StockItem>> ViewsAsync(IReadOnlyList<Item> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var goldList = await gold.AllAsync(ct);
        return list.Select(i => Shop.View(i, goldList)).ToList();
    }
}

/// <summary>The links from Lager's pages to Salg (Ordrer, Tilbud) - the pages ask this, not the settings.</summary>
public sealed class SalgLinks(LagerOptions options)
{
    public string Base => options.SalgPublicUrl.TrimEnd('/');
    public string Orders => Base + "/";
    public string Offers => Base + "/tilbud";
}
