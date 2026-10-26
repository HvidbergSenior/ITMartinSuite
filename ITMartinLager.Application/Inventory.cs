using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Boxes: numbering, places, contents.</summary>
public sealed class BoxUseCases(IBoxRepository boxes, IItemRepository items, IIntakeRepository? intake = null)
{
    public Task<Box> NewAsync(string place, CancellationToken ct = default) => boxes.AddNextAsync(place.Trim(), ct);
    public Task<List<Box>> RecentAsync(int count, CancellationToken ct = default) => boxes.RecentAsync(count, ct);
    public Task<Box?> ByCodeAsync(string code, CancellationToken ct = default) => boxes.ByCodeAsync(code, ct);

    /// <summary>A label sheet for boxes from..to (at most BoxCodes.MaxSheet): the boxes are made when missing, so every QR opens a box.</summary>
    public async Task<List<Box>> LabelSheetAsync(int from, int to, CancellationToken ct = default)
    {
        var list = new List<Box>();
        foreach (var code in BoxCodes.Range(from, to)) list.Add(await boxes.GetOrAddAsync(code, ct));
        return list;
    }
    /// <summary>A label sheet for the boxes of a Vareinfo session (2026-10-26: "after a session with 20 boxes ... an easy
    /// way to make qr labels on each"): "K0021~Anders And,K0022~Bøger" - made when missing, the name kept as the box's note
    /// while it has none.</summary>
    public async Task<List<Box>> LabelSheetAsync(string? sheet, CancellationToken ct = default)
    {
        var list = new List<Box>();
        foreach (var part in (sheet ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(BoxCodes.MaxSheet))
        {
            var (code, name) = part.IndexOf('~') is var i and > 0 ? (part[..i], part[(i + 1)..].Trim()) : (part, "");
            var n = BoxCodes.Number(code);
            if (n is < 1 or >= BoxCodes.FirstTest) continue;
            var box = await boxes.GetOrAddAsync(BoxCodes.For(n), ct);
            if (name != "" && box.Note == "") { await boxes.SetNoteAsync(box.Id, name, ct); box.Note = name; }
            list.Add(box);
        }
        return list;
    }

    public Task<List<BoxSummary>> AllAsync(CancellationToken ct = default) => boxes.SummariesAsync(ct);

    /// <summary>Admin (user 2026-10-26: "as admin user i should be able to delete kasser etc"): the box, the things in it
    /// and what Vareinfo sent for it are gone. The page asks first and says how many things. Returns how many things went.</summary>
    public async Task<int> DeleteAsync(string code, CancellationToken ct = default)
    {
        if (await boxes.ByCodeAsync(code, ct) is not { } box) return 0;
        var inBox = await items.InBoxAsync(box.Id, ct);
        foreach (var i in inBox) await items.DeleteAsync(i.Id, ct);
        if (intake is not null) await intake.ForgetBoxAsync(box.Code, ct);
        await boxes.DeleteAsync(box.Id, ct);
        return inBox.Count;
    }
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
    public async Task<(List<Item> Hits, Dictionary<int, PriceFind> FarAbove)?> SearchAsync(string text, string? kind, string? valuation = null, CancellationToken ct = default)
    {
        if (text.Trim().Length < 2 && kind is null && valuation is null) return null;
        var hits = (await items.SearchAsync(text, kind, valuation, MaxHits, ct))
            .OrderBy(i => i.Series).ThenBy(i => i.Year).ThenBy(i => i.Number).ThenBy(i => i.Title).ToList();
        var priced = hits.Where(i => i.PriceHint is > 0).ToDictionary(i => i.Id);
        var tops = await prices.TopFindsAsync(priced.Keys.ToList(), ct);
        return (hits, tops.Where(t => t.Value.Kr >= priced[t.Key].PriceHint * 3).ToDictionary(t => t.Key, t => t.Value));
    }

    /// <summary>Every unsold item with its "worth looking into?" line - gold first (user 2026-10-12: "so that I can go
    /// through all interesting in one go").</summary>
    public async Task<List<(Item Item, string Note)>> InterestingAsync(IReadOnlyList<GoldEntry> gold, CancellationToken ct = default) =>
        (await items.AllAsync(ct)).Where(i => i.Status != Statuses.Solgt)
            .Select(i => (Item: i, Note: Gold.Interesting(gold, i.Kind, i.Title, i.Series, i.Number, i.Year, i.Price ?? i.PriceHint, i.Want, i.Have)))
            .Where(x => x.Note is not null).Select(x => (x.Item, x.Note!))
            .OrderBy(x => Gold.Rank(x.Item2)).ThenByDescending(x => x.Item.Price ?? x.Item.PriceHint ?? 0).ToList();

    public async Task<ItemView?> GetAsync(int id, CancellationToken ct = default)
    {
        var item = await items.GetAsync(id, ct);
        if (item is null) return null;
        var (finds, checks) = await prices.ForItemAsync(id, ct);
        return new ItemView(item, finds, checks);
    }

    /// <summary>"Vi har flere" (user 2026-10-15: "we have more than one ... and the box numbers"): the other unsold rows
    /// of what looks like the same thing, with their boxes.</summary>
    public async Task<List<Item>> OthersLikeAsync(Item item, CancellationToken ct = default) =>
        (await items.WithTitlesAsync([item.Title], ct, item.Barcode != "" ? [item.Barcode] : null))
            .Where(i => i.Status != Statuses.Solgt && i.LooksLike(item))
            .OrderBy(i => i.Box?.Code ?? "~").ToList();

    /// <summary>Saves a corrected item. A changed title, number or ISBN = the old prices are about another book,
    /// so the hunter takes it next.</summary>
    public async Task SaveAsync(Item item, CancellationToken ct = default)
    {
        var before = await items.GetAsync(item.Id, ct) ?? throw new LagerException("Varen findes ikke længere.");
        item.Kind = Kinds.Normalise(item.Kind);
        item.Condition = Conditions.Normalise(item.Condition);
        item.Usage = Usages.Normalise(item.Usage);
        if (item.Usage != before.Usage) item.PricesAt = null;   // new is priced against new shops, used against used markets
        item.EditionHint = EditionHints.Normalise(item.EditionHint);
        item.Barcode = Barcodes.ToIsbn13(item.Barcode ?? "");   // ISBN-10 (also ending in X) becomes ISBN-13
        if (Pricing.SearchKey(item) != Pricing.SearchKey(before)) item.PricesAt = null;
        item.Touch();
        await items.UpdateAsync([item], ct);
    }

    /// <summary>"Vis på forsiden" on or off.</summary>
    public async Task<Item> FeatureAsync(int id, bool on, CancellationToken ct = default)
    {
        var item = await items.GetAsync(id, ct) ?? throw new LagerException("Varen findes ikke længere.");
        item.FeaturedAt = on ? DateTime.UtcNow : null;
        item.Touch();
        await items.UpdateAsync([item], ct);
        return item;
    }

    public Task DeleteAsync(int id, CancellationToken ct = default) => items.DeleteAsync(id, ct);

    /// <summary>The file of the item's own photo, or null. (Pile and kolofon photos are kept in Vareinfo.)</summary>
    public async Task<string?> PhotoFileAsync(int id, CancellationToken ct = default) =>
        await items.GetAsync(id, ct) is { } i ? photos.FullPath(i.ItemPhoto) : null;

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
