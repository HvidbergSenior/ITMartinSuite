using ITMartinLager.Domain;

namespace ITMartinLager.Application;

// Stikprøve (user 2026-11-24): 20 random things for sale with the used listings behind their price; Martin marks each Rigtig or
// Forkert. Forkert = that listing never counts again, and the thing's price is worked out again at once. PerSource shows how good
// the matches are.
public sealed class SpotCheckUseCases(IItemRepository items, IPriceRepository prices)
{
    public sealed record Row(Item Item, List<PriceFind> Finds, Dictionary<string, bool> Marks, int? Standard);

    /// <summary>A new random sample - or the same things again (ids from the address), so going back from a product keeps the list.</summary>
    public async Task<List<Row>> SampleAsync(int count = 20, IReadOnlyCollection<int>? same = null, CancellationToken ct = default)
    {
        List<Item> pick;
        if (same is { Count: > 0 })
        {
            var order = same.ToList();
            pick = (await items.ByIdsAsync(order, ct)).OrderBy(i => order.IndexOf(i.Id)).ToList();
        }
        else
        {
            var withFinds = (await prices.ItemsWithUsedFindsAsync(ct)).ToHashSet();
            var listed = (await items.AllListedAsync(ct)).Where(i => withFinds.Contains(i.Id) && i.Kind != Kinds.Ting && i.Kind != Kinds.Strik).ToList();
            pick = listed.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
        }
        var ids = pick.Select(i => i.Id).ToList();
        var verdicts = (await prices.VerdictsForAsync(ids, ct)).ToLookup(v => v.ItemId);
        var rows = new List<Row>();
        foreach (var i in pick)
        {
            // Every used, exact listing - also the ones already marked Forkert, so a mark can be changed.
            var (finds, _) = await prices.ForItemAsync(i.Id, ct);
            var used = finds.Where(f => !f.IsNew && !f.Similar && f.Kr > 0).OrderByDescending(f => f.Kr).ToList();
            rows.Add(new Row(i, used, verdicts[i.Id].ToDictionary(v => v.ListingKey, v => v.Right), FixedPrice.StandardOf(i)));
        }
        return rows;
    }

    /// <summary>Marks one listing; the thing's price is worked out again with the Forkert ones left out. Returns the new hint.</summary>
    public async Task<decimal?> MarkAsync(int itemId, PriceFind find, bool right, CancellationToken ct = default)
    {
        await prices.SetVerdictAsync(new ListingVerdict
        {
            ItemId = itemId, ListingKey = ListingVerdict.KeyOf(find), Source = find.Source, Name = find.Name, Kr = find.Kr, Right = right,
        }, ct);
        if (await items.GetAsync(itemId, ct) is not { } item) return null;
        if (!FixedPrice.Applies(item) || item.PricesAt is null) return item.PriceHint;
        var finds = (await prices.FindsForAsync([itemId], ct)).GetValueOrDefault(itemId) ?? [];
        var hint = FixedPrice.Hint(item, finds, item.PriceHint);
        if (hint != item.PriceHint) { item.PriceHint = hint; item.Touch(); await items.UpdateAsync([item], ct); }
        return hint;
    }

    public async Task<List<(string Source, int Right, int Wrong)>> StatsAsync(CancellationToken ct = default) =>
        ListingVerdict.PerSource(await prices.AllVerdictsAsync(ct));
}
