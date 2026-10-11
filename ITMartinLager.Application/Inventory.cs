using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Boxes: numbering, places, contents.</summary>
public sealed class BoxUseCases(IBoxRepository boxes, IItemRepository items)
{
    public Task<Box> NewAsync(string place, CancellationToken ct = default) => boxes.AddNextAsync(place.Trim(), ct);
    public Task<List<Box>> RecentAsync(int count, CancellationToken ct = default) => boxes.RecentAsync(count, ct);
    public Task<Box?> ByCodeAsync(string code, CancellationToken ct = default) => boxes.ByCodeAsync(code, ct);
    public Task<List<BoxSummary>> AllAsync(CancellationToken ct = default) => boxes.SummariesAsync(ct);
    public Task SetPlaceAsync(int id, string place, CancellationToken ct = default) => boxes.SetPlaceAsync(id, place.Trim(), ct);

    public async Task<(Box? Box, List<Item> Items)> ContentsAsync(string code, CancellationToken ct = default)
    {
        var box = await boxes.ByCodeAsync(code, ct);
        if (box is null) return (null, []);
        var inBox = await items.InBoxAsync(box.Id, ct);
        return (box, inBox.OrderBy(i => i.Kind).ThenBy(i => i.Series).ThenBy(i => i.Year).ThenBy(i => i.Number).ToList());
    }

    public async Task<(int Units, int Boxes)> StockAsync(CancellationToken ct = default) =>
        (await items.UnitsAsync(ct), await boxes.CountAsync(ct));
}

public sealed record ItemView(Item Item, List<PriceFind> Finds, List<PriceCheck> Checks);

/// <summary>Search, look at, correct and delete items.</summary>
public sealed class ItemUseCases(IItemRepository items, IPriceRepository prices, IPhotoStore photos)
{
    public const int MaxHits = 200;

    /// <summary>Every word must match somewhere. Also the dearest find per hit when it is 3x the normal price or more
    /// (user: "if the normal price is 5 kr but on amazon.de it costs 50 kr i want to know that").</summary>
    public async Task<(List<Item> Hits, Dictionary<int, PriceFind> FarAbove)?> SearchAsync(string text, string? kind, CancellationToken ct = default)
    {
        if (text.Trim().Length < 2 && kind is null) return null;
        var hits = (await items.SearchAsync(text, kind, MaxHits, ct))
            .OrderBy(i => i.Series).ThenBy(i => i.Year).ThenBy(i => i.Number).ThenBy(i => i.Title).ToList();
        var priced = hits.Where(i => i.PriceHint is > 0).ToDictionary(i => i.Id);
        var tops = await prices.TopFindsAsync(priced.Keys.ToList(), ct);
        return (hits, tops.Where(t => t.Value.Kr >= priced[t.Key].PriceHint * 3).ToDictionary(t => t.Key, t => t.Value));
    }

    public async Task<ItemView?> GetAsync(int id, CancellationToken ct = default)
    {
        var item = await items.GetAsync(id, ct);
        if (item is null) return null;
        var (finds, checks) = await prices.ForItemAsync(id, ct);
        return new ItemView(item, finds, checks);
    }

    /// <summary>Saves a corrected item. A changed title, number or ISBN = the old prices are about another book,
    /// so the hunter takes it next.</summary>
    public async Task SaveAsync(Item item, CancellationToken ct = default)
    {
        var before = await items.GetAsync(item.Id, ct) ?? throw new LagerException("Varen findes ikke længere.");
        item.Kind = Kinds.Normalise(item.Kind);
        item.Condition = Conditions.Normalise(item.Condition);
        item.EditionHint = EditionHints.Normalise(item.EditionHint);
        item.Barcode = Barcodes.ToIsbn13(item.Barcode ?? "");   // ISBN-10 (also ending in X) becomes ISBN-13
        if (Pricing.SearchKey(item) != Pricing.SearchKey(before)) item.PricesAt = null;
        item.Touch();
        await items.UpdateAsync([item], ct);
    }

    public Task DeleteAsync(int id, CancellationToken ct = default) => items.DeleteAsync(id, ct);

    /// <summary>The file of the item's own photo (kolofon: true = its kolofon photo), or null.</summary>
    public async Task<string?> PhotoFileAsync(int id, bool kolofon, CancellationToken ct = default) =>
        await items.GetAsync(id, ct) is { } i ? photos.FullPath(kolofon ? i.KolofonPhoto : i.ItemPhoto) : null;

    /// <summary>A photo of just this item, for auctions and the shop.</summary>
    public async Task<string> SavePhotoAsync(int id, Stream photo, CancellationToken ct = default)
    {
        var item = await items.GetAsync(id, ct) ?? throw new LagerException("Varen findes ikke længere.");
        var jpeg = await photos.ShrinkAsync(photo, ct);
        var path = Path.Combine("photos", "items", $"{id}.jpg");
        await photos.SaveAsync(path, jpeg, ct);
        item.ItemPhoto = path;
        item.Touch();
        await items.UpdateAsync([item], ct);
        return path;
    }
}
