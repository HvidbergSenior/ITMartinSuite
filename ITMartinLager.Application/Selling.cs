using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Live Auktion: open auctions, sending an item, and copying results back (Live Auktion deletes an auction
/// 7 days after it was created - Lager keeps the result).</summary>
public sealed class AuctionUseCases(IAuctionGateway auctions, IItemRepository items, IPhotoStore photos, TimeProvider clock)
{
    public bool Enabled => auctions.Enabled;
    public string PublicBase => auctions.PublicBase;

    public Task<List<AuctionSessionInfo>> SessionsAsync(CancellationToken ct = default) => auctions.SessionsAsync(ct);

    public async Task<Item> SendAsync(int itemId, string code, decimal startPrice, int lot, CancellationToken ct = default)
    {
        var item = await items.GetAsync(itemId, ct) ?? throw new LagerException("Varen findes ikke længere.");
        if (code == "") throw new LagerException("Vælg en auktion.");
        var jpeg = item.ItemPhoto != "" ? await photos.ReadAsync(item.ItemPhoto, ct) : null;
        lot = Math.Clamp(lot, 1, Math.Max(1, item.Quantity));
        var id = await auctions.SendAsync(code, item.Line(), AuctionResult.Description(item), Math.Max(0, startPrice), lot, jpeg, ct);
        AuctionResult.Sent(item, id, code);
        await items.UpdateAsync([item], ct);
        return item;
    }

    /// <summary>Results for the items up for auction. Returns how many changed.</summary>
    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        if (!auctions.Enabled) return 0;
        var open = await items.OnAuctionAsync(ct);
        if (open.Count == 0) return 0;
        var ids = open.Select(i => Guid.TryParse(i.AuctionItemId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
        var now = clock.GetLocalNow().DateTime;
        var changed = new List<Item>();
        foreach (var s in await auctions.StatusAsync(ids, ct))
            if (open.FirstOrDefault(i => i.AuctionItemId == s.Id.ToString()) is { } item && AuctionResult.Apply(item, s, now))
                changed.Add(item);
        await items.UpdateAsync(changed, ct);
        return changed.Count;
    }
}

/// <summary>Magic cards from the MTG Scanner into the register. Fetching again updates the same rows - no duplicates.</summary>
public sealed class MagicImportUseCases(IMagicGateway magic, IItemRepository items, IBoxRepository boxes)
{
    public bool Enabled => magic.Enabled;

    public Task<List<MagicOwner>> OwnersAsync(CancellationToken ct = default) => magic.OwnersAsync(ct);

    /// <summary>boxId 0 = a new box - but only when there is something new: cards fetched before stay where they are,
    /// and a re-fetch with the default choice must not leave empty boxes behind.</summary>
    public async Task<(int Added, int Updated, Box Box)> ImportAsync(string owner, int boxId, CancellationToken ct = default)
    {
        var cards = await magic.CardsAsync(owner, ct);
        var refs = cards.Select(c => c.Id.ToString()).ToList();
        var existing = (await items.ByMagicRefsAsync(refs, ct)).ToDictionary(i => i.MagicRef);
        var box = boxId != 0 ? await boxes.GetAsync(boxId, ct)
            : existing.Values.FirstOrDefault(i => i.BoxId is not null) is { BoxId: { } known } ? await boxes.GetAsync(known, ct)
            : await boxes.AddNextAsync("Magic-kort", ct);
        if (box is null) throw new LagerException("Kassen findes ikke.");

        var added = new List<Item>();
        var updated = new List<Item>();
        foreach (var c in cards)
        {
            if (existing.TryGetValue(c.Id.ToString(), out var item)) { if (MagicCards.Update(item, c)) updated.Add(item); }
            else added.Add(MagicCards.NewItem(c, box.Id));
        }
        await items.AddAsync(added, ct);
        await items.UpdateAsync(updated, ct);
        return (added.Count, updated.Count, box);
    }
}
