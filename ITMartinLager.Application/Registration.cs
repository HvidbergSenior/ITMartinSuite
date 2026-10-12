using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Taking things into stock. What they ARE is found out in Vareinfo (photo of a pile, barcodes) - Lager gets
/// the result through /api/info and from the Bøger page (2026-10-12 split). No box needed (user 2026-10-12: choosing
/// a box is "way to difficult") - a box is only an optional "where is it".</summary>
public sealed class RegistrationUseCases(IItemRepository items)
{
    /// <summary>Same thing in the same condition = one row with a higher quantity (in the box, when one is given;
    /// anywhere in stock otherwise). Vareinfo only sends a thing we already have when the person said "+1 eksemplar".</summary>
    public async Task<(int Added, int Merged)> SaveAsync(int? boxId, int? photoId, IEnumerable<Found> found, CancellationToken ct = default)
    {
        var list = found.ToList();
        var stock = boxId is { } b ? await items.InBoxAsync(b, ct) : await items.WithTitlesAsync(list.Select(f => f.Title).ToList(), ct);
        var added = new List<Item>();
        var merged = new List<Item>();
        foreach (var f in list)
        {
            var same = stock.FirstOrDefault(i => i.SameAs(f));
            if (same is not null)
            {
                same.Quantity += f.Quantity;
                same.Touch();
                if (!merged.Contains(same) && !added.Contains(same)) merged.Add(same);
                continue;
            }
            var item = Item.From(f, boxId, photoId);
            stock.Add(item);
            added.Add(item);
        }
        await items.AddAsync(added, ct);
        await items.UpdateAsync(merged, ct);
        return (added.Count, list.Count - added.Count);
    }

    /// <summary>For each thing: how many we already have of exactly that (0 = new), and which item it is.</summary>
    public async Task<List<(int Quantity, int? ItemId)>> InStockAsync(IReadOnlyList<Found> found, CancellationToken ct = default)
    {
        var stock = await items.WithTitlesAsync(found.Select(f => f.Title).ToList(), ct);
        return found.Select(f => stock.FirstOrDefault(i => i.SameAs(f)) is { } i ? (i.Quantity, (int?)i.Id) : (0, null)).ToList();
    }
}
