using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Getting things onto the register: a photo of a pile (ONE AI call reads every cover), barcodes, and saving
/// what was read into a box.</summary>
public sealed class RegistrationUseCases(
    ICoverReader reader, IPhotoStore photos, IPhotoRepository photoRows, IItemRepository items, IBarcodeLookup barcodes,
    LagerOptions options, TimeProvider clock)
{
    public const int MaxInterestPerCall = 40;

    public bool AutoInterest => options.AutoInterest;
    public int MaxCallsPerDay => reader.MaxCallsPerDay;
    public Task<int> CallsTodayAsync(CancellationToken ct = default) => reader.CallsTodayAsync(ct);

    /// <summary>Shrinks and keeps the photo (so a wrong reading can be checked against it), then reads it.</summary>
    public async Task<(int PhotoId, List<Found> Found)> ReadPileAsync(Stream photo, string hint, int? boxId, string label, CancellationToken ct = default)
    {
        var jpeg = await photos.ShrinkAsync(photo, ct);
        var now = clock.GetLocalNow();
        var path = Path.Combine("photos", now.ToString("yyyy-MM"), $"{now:ddHHmmssfff}-{label}.jpg");
        await photos.SaveAsync(path, jpeg, ct);
        var found = await reader.ReadPileAsync(jpeg, hint, ct);
        var row = await photoRows.AddAsync(new Photo { Path = path, BoxId = boxId, Found = found.Count }, ct);
        return (row.Id, found);
    }

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

    /// <summary>"Det interessante" for up to 40 items in ONE call. Index -> text.</summary>
    public Task<Dictionary<int, string>> WriteInterestAsync(IReadOnlyList<string> lines, CancellationToken ct = default) =>
        reader.WriteInterestAsync(lines.Take(MaxInterestPerCall).ToList(), ct);

    /// <summary>The file of a registration photo, so a wrong reading can be checked against the picture.</summary>
    public async Task<string?> PhotoFileAsync(int photoId, CancellationToken ct = default) =>
        await photoRows.GetAsync(photoId, ct) is { } p ? photos.FullPath(p.Path) : null;

    public Task<Found?> LookupBarcodeAsync(string code, CancellationToken ct = default) => barcodes.LookupAsync(code, ct);
}
