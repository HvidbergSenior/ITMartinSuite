using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Taking things into a box. What they ARE is found out in Vareinfo (photo of a pile, barcodes) - Lager gets
/// the result through /api/info and from the Bøger page (2026-10-12 split).</summary>
public sealed class RegistrationUseCases(IItemRepository items)
{
    /// <summary>Same thing, same condition, same box = one row with a higher quantity.</summary>
    public async Task<(int Added, int Merged)> SaveAsync(int boxId, int? photoId, IEnumerable<Found> found, CancellationToken ct = default)
    {
        var inBox = await items.InBoxAsync(boxId, ct);
        var added = new List<Item>();
        var merged = new List<Item>();
        foreach (var f in found)
        {
            var same = inBox.FirstOrDefault(i => i.SameAs(f));
            if (same is not null)
            {
                same.Quantity += f.Quantity;
                same.Touch();
                if (!merged.Contains(same) && !added.Contains(same)) merged.Add(same);
                continue;
            }
            var item = Item.From(f, boxId, photoId);
            inBox.Add(item);
            added.Add(item);
        }
        await items.AddAsync(added, ct);
        await items.UpdateAsync(merged, ct);
        return (added.Count, found.Count() - added.Count);
    }
}
