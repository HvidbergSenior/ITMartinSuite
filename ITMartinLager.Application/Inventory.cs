using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Boxes: numbering, places, contents.</summary>
public sealed class BoxUseCases(IBoxRepository boxes, IItemRepository items, IIntakeRepository? intake = null)
{
    public Task<Box> NewAsync(string place, CancellationToken ct = default) => boxes.AddNextAsync(place.Trim(), ct);
    public Task<List<Box>> RecentAsync(int count, CancellationToken ct = default) => boxes.RecentAsync(count, ct);
    public Task<Box?> ByCodeAsync(string code, CancellationToken ct = default) => boxes.ByCodeAsync(code, ct);

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

    /// <summary>Lager's job (user 2026-11-15: "correct ones that are a little difficult"): the things the AI was unsure of.
    /// Gem sets the confidence to 1, so a fixed thing leaves the list by itself.</summary>
    public Task<List<int>> UnsureAsync(CancellationToken ct = default) => items.UnsureIdsAsync(500, ct);

    /// <summary>The next difficult one after this id (round again from the start), or null when there are none left but this one.</summary>
    public async Task<int?> NextUnsureAsync(int afterId, CancellationToken ct = default)
    {
        var ids = await UnsureAsync(ct);
        return ids.FirstOrDefault(i => i > afterId) is var next and > 0 ? next : ids.FirstOrDefault(i => i != afterId) is var first and > 0 ? first : null;
    }

    /// <summary>Every word must match somewhere. Also the dearest find per hit when it is 3x the normal price or more
    /// (user: "if the normal price is 5 kr but on amazon.de it costs 50 kr i want to know that").</summary>
    public async Task<(List<Item> Hits, Dictionary<int, PriceFind> FarAbove, int Total)?> SearchAsync(string text, string? kind, string? valuation = null, CancellationToken ct = default, ItemScope? scope = null)
    {
        scope ??= ItemScope.All;
        if (text.Trim().Length < 2 && kind is null && valuation is null && scope.IsAll) return null;
        var hits = (await items.SearchAsync(text, kind, valuation, MaxHits, ct, scope))
            .OrderBy(i => i.Series).ThenBy(i => i.Year).ThenBy(i => i.Number).ThenBy(i => i.Title).ToList();
        var priced = hits.Where(i => i.PriceHint is > 0).ToDictionary(i => i.Id);
        var tops = await prices.TopFindsAsync(priced.Keys.ToList(), ct);
        var total = hits.Count < MaxHits ? hits.Count : await items.CountAsync(text, kind, valuation, ct, scope);
        return (hits, tops.Where(t => t.Value.Kr >= priced[t.Key].PriceHint * 3).ToDictionary(t => t.Key, t => t.Value), total);
    }

    /// <summary>Every unsold item with its "worth looking into?" line - gold first (user 2026-10-12: "so that I can go
    /// through all interesting in one go").</summary>
    public async Task<List<(Item Item, string Note)>> InterestingAsync(IReadOnlyList<GoldEntry> gold, CancellationToken ct = default, ItemScope? scope = null) =>
        (await items.AllAsync(ct)).Where(i => i.Status != Statuses.Solgt && Statuses.Ours(i.Status) && (scope ?? ItemScope.All).Holds(i))
            .Select(i => (Item: i, Note: Gold.Interesting(gold, i.Kind, i.Title, i.Series, i.Number, i.Year, i.Price ?? i.PriceHint, i.Want, i.Have)))
            .Where(x => x.Note is not null).Select(x => (x.Item, x.Note!))
            .OrderBy(x => Gold.Rank(x.Item2)).ThenByDescending(x => x.Item.Price ?? x.Item.PriceHint ?? 0).ToList();

    /// <summary>Puts every item in its main category and language (Item.Classify) - plain rules, no AI; the job runs it every
    /// round, so rules changed in a new version reach the old rows. Returns how many changed.</summary>
    public async Task<int> RefreshCategoriesAsync(CancellationToken ct = default)
    {
        var changed = (await items.AllAsync(ct)).Where(i => i.Classify()).ToList();
        if (changed.Count > 0) await items.UpdateAsync(changed, ct);
        return changed.Count;
    }

    /// <summary>Boxes that got new items since this moment (the "Hvilke varer" chips on Søg).</summary>
    public Task<List<(Box Box, int Count)>> NewPerBoxAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        items.NewPerBoxAsync(sinceUtc, ct);

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
        // A person has looked at it (the item page says "tjek den mod billedet"): no longer unsure - it may go on
        // bogshoppen.dk and into a deal (user 2026-10-31: "low confidence should be manual investigation").
        item.Confidence = Math.Max(item.Confidence, 1);
        item.Touch();
        await items.UpdateAsync([item], ct);
    }

    /// <summary>"Fjern" in the list (user 2026-11-15, misreadings like "..es"): the item leaves the stock but is kept as
    /// Ikke købt - never deleted - so Fortryd brings it back. Returns the status it had.</summary>
    public async Task<string> RemoveAsync(int id, CancellationToken ct = default)
    {
        var item = await items.GetAsync(id, ct) ?? throw new LagerException("Varen findes ikke længere.");
        var before = item.Status;
        item.Status = Statuses.IkkeKoebt;
        item.Touch();
        await items.UpdateAsync([item], ct);
        return before;
    }

    /// <summary>Fortryd after Fjern: the item gets the status it had back.</summary>
    public async Task UndoRemoveAsync(int id, string status, CancellationToken ct = default)
    {
        var item = await items.GetAsync(id, ct) ?? throw new LagerException("Varen findes ikke længere.");
        if (item.Status != Statuses.IkkeKoebt || !Statuses.All.Contains(status)) return;
        item.Status = status;
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
    /// <summary>Photo n of the item: 1 = the main one, 2.. = the extra ones.</summary>
    public async Task<string?> PhotoFileAsync(int id, int n, CancellationToken ct = default)
    {
        if (n <= 1) return await PhotoFileAsync(id, ct);
        return await items.GetAsync(id, ct) is { ItemPhoto.Length: > 0 } i && n <= i.MorePhotos + 1
            ? photos.FullPath(Path.Combine("photos", "items", $"{id}-{n}.jpg")) : null;
    }

    /// <summary>One more photo of the same thing; the first photo always becomes the main one.</summary>
    public async Task<int> AddPhotoAsync(int id, Stream photo, CancellationToken ct = default)
    {
        var item = await items.GetAsync(id, ct) ?? throw new LagerException("Varen findes ikke længere.");
        if (item.ItemPhoto == "") { await SavePhotoAsync(id, photo, ct); return 1; }
        if (item.MorePhotos >= Item.MaxMorePhotos) throw new LagerException($"Der kan højst være {Item.MaxMorePhotos + 1} billeder af en ting.");
        var n = item.MorePhotos + 2;
        await photos.SaveAsync(Path.Combine("photos", "items", $"{id}-{n}.jpg"), await photos.ShrinkAsync(photo, ct), ct);
        item.MorePhotos++;
        item.Touch();
        await items.UpdateAsync([item], ct);
        return n;
    }

    public async Task<string?> PhotoFileAsync(int id, CancellationToken ct = default) =>
        await items.GetAsync(id, ct) is { } i ? photos.FullPath(i.ItemPhoto) : null;

    /// <summary>The kinds a maker or an owner sells through ITMartin (Item.Seller): Strikkeshoppen and Tingshoppen (2026-11-10).</summary>
    public static readonly string[] SellerKinds = [Kinds.Strik, Kinds.Ting];

    /// <summary>Strikkeshoppen (2026-11-10): a hand-knitted thing a maker sells through ITMartin.</summary>
    public Task<Item> AddHandmadeAsync(string? title, string? genre, decimal? price, string? seller, string? note,
        Stream? photo, CancellationToken ct = default) => AddForSellerAsync(Kinds.Strik, title, genre, price, seller, note, photo, ct);

    /// <summary>Strikkeshoppen / Tingshoppen (2026-11-10): a thing someone else owns and Martin sells for them. A person sets what
    /// it is, the price (or none yet) and whose it is - no AI, no price hunt (neither is a hunted kind). For sale at once.
    /// Strik is new (hand-made); Ting is used (from a dødsbo or a loft).</summary>
    public Task<Item> AddForSellerAsync(string kind, string? title, string? genre, decimal? price, string? seller, string? note,
        Stream? photo, CancellationToken ct = default) =>
        AddForSellerWithPhotosAsync(kind, title, genre, price, seller, note, photo is null ? [] : [photo], ct);

    /// <summary>The same with several photos - the first is the main one, the rest are extra photos of the same thing.</summary>
    public async Task<Item> AddForSellerWithPhotosAsync(string kind, string? title, string? genre, decimal? price, string? seller, string? note,
        IReadOnlyList<Stream> photos, CancellationToken ct = default)
    {
        if (!SellerKinds.Contains(kind)) throw new LagerException("Den slags kan ikke lægges ind her.");
        var strik = kind == Kinds.Strik;
        var t = (title ?? "").Trim();
        if (t.Length == 0) throw new LagerException(strik ? "Skriv, hvad det er – fx Blå sweater med snoninger." : "Skriv, hvad det er – fx Rubinglas-vase, böhmisk.");
        var list = strik ? Genres.Strik : Genres.Ting;
        if (list.FirstOrDefault(g => string.Equals(g, genre?.Trim(), StringComparison.OrdinalIgnoreCase)) is not { } g)
            throw new LagerException(strik ? "Vælg, hvilken slags strik det er." : "Vælg, hvilken slags ting det er.");
        // No price yet is fine (user 2026-11-10: "no prices yet") - it is set later on the item, like any other thing.
        if (price is < 0) throw new LagerException("Prisen kan ikke være under 0 kr.");
        var who = (seller ?? "").Trim();
        if (who.Length == 0) throw new LagerException(strik ? "Skriv, hvem der har strikket det – så ved vi, hvem pengene skal til."
            : "Skriv, hvem tingen er fra – så ved vi, hvem pengene skal til.");
        var item = new Item
        {
            Kind = kind, Title = t.Length > 200 ? t[..200] : t, Genre = g, GenreAt = DateTime.UtcNow, Price = price is > 0 ? Math.Round(price.Value) : null,
            Seller = who.Length > 80 ? who[..80] : who, Usage = strik ? Usages.New : Usages.Used, Condition = strik ? "Ny" : "God",
            // The text typed here is for buyers (2026-11-10: "why no description?" - it sat in Note, which the shop never shows).
            Note = (note ?? "").Trim(), Interest = (note ?? "").Trim(), Language = Languages.Dansk, Status = Statuses.Lager,
        };
        item.Touch();
        await items.AddAsync([item], ct);
        foreach (var p in photos.Take(Item.MaxMorePhotos + 1)) await AddPhotoAsync(item.Id, p, ct);
        return photos.Count == 0 ? item : await items.GetAsync(item.Id, ct) ?? item;
    }

    /// <summary>Every thing of one seller kind (for sale and sold), newest first - the Strik and Ting pages in Lager.</summary>
    public Task<List<Item>> HandmadeAsync(CancellationToken ct = default) => items.OfKindAsync(Kinds.Strik, ct);
    public Task<List<Item>> ForSellerAsync(string kind, CancellationToken ct = default) => items.OfKindAsync(kind, ct);

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
