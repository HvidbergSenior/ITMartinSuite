using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>A wrong cut-out made right by a person (user 2026-11-16: "the picture is wrong" - the reader's box around a DVD
/// spine was loose, so the cut-out showed a strip of several DVDs). The person sees the whole pile photo it was read from,
/// drags a box around the thing, and that becomes its photo. The photo before is kept ({id}-original.jpg, the same as for a
/// painted-over price mark), so ItemUseCases.UncoverAsync brings it back.</summary>
public sealed class ItemPhotoUseCases(IItemRepository items, IPhotoStore photos, IVareinfoGateway? vareinfo = null)
{
    /// <summary>Turn the thing's own photo (user 2026-11-16: "add a way to rotate pictures" + "could be manually"): a quarter
    /// clockwise (90) or back (270). The photo before is kept the first time, so Fortryd brings it back. Null = done.</summary>
    public async Task<string?> RotateAsync(int id, int degrees, CancellationToken ct = default)
    {
        if (degrees is not (90 or 180 or 270)) return "Drej 90, 180 eller 270 grader.";
        if (await items.GetAsync(id, ct) is not { } item) return "Varen findes ikke.";
        if (item.ItemPhoto == "" || await photos.ReadAsync(item.ItemPhoto, ct) is not { } jpeg)
            return "Tingen har kun billedet af hele bunken – lav først et udsnit med »Ret udsnittet«.";
        if (await photos.ReadAsync(ItemUseCases.OriginalPath(id), ct) is null) await photos.SaveAsync(ItemUseCases.OriginalPath(id), jpeg, ct);
        await photos.SaveAsync(item.ItemPhoto, await photos.RotateAsync(jpeg, degrees, ct), ct);
        item.Touch();   // PhotoCheckJob measures it again (width and height swap)
        await items.UpdateAsync([item], ct);
        return null;
    }

    /// <summary>Measure the photos that are new or changed (PhotoCheckJob, no AI - only the file headers). Returns how many.
    /// UpdatedAt is kept, so measuring is not itself a change.</summary>
    public async Task<int> MeasureAsync(int max = 500, CancellationToken ct = default)
    {
        var todo = await items.PhotosToMeasureAsync(max, ct);
        foreach (var i in todo)
        {
            var size = await photos.SizeAsync(i.ItemPhoto, ct);
            i.PhotoW = size?.W; i.PhotoH = size?.H;
            i.PhotoMeasuredAt = i.UpdatedAt > DateTime.UtcNow ? i.UpdatedAt : DateTime.UtcNow;
        }
        if (todo.Count > 0) await items.UpdateAsync(todo, ct);
        return todo.Count;
    }

    /// <summary>The pile photo the thing was read from (Vareinfo's reading copy), or null.</summary>
    public async Task<byte[]?> PileAsync(int id, CancellationToken ct = default) =>
        vareinfo is not null && await items.GetAsync(id, ct) is { PhotoId: { } photo } ? await vareinfo.PhotoAsync(photo, null, ct) : null;

    /// <summary>Cut the thing out of its pile photo again. Null = done, else the (Danish) reason.</summary>
    public async Task<string?> RecutAsync(int id, PhotoArea area, CancellationToken ct = default)
    {
        if (await items.GetAsync(id, ct) is not { } item) return "Varen findes ikke.";
        if (await PileAsync(id, ct) is not { } pile) return "Billedet af bunken findes ikke længere.";
        if (await photos.CropAsync(pile, area, ct) is not { } cut) return "Firkanten er for lille – træk den rundt om hele tingen.";
        var path = item.ItemPhoto != "" ? item.ItemPhoto : Path.Combine("photos", "items", $"{id}.jpg");
        if (item.ItemPhoto != "" && await photos.ReadAsync(ItemUseCases.OriginalPath(id), ct) is null
            && await photos.ReadAsync(item.ItemPhoto, ct) is { } before)
            await photos.SaveAsync(ItemUseCases.OriginalPath(id), before, ct);
        await photos.SaveAsync(path, cut, ct);
        item.ItemPhoto = path;
        item.PileArea = Item.AreaText(area);   // the person showed where it is - remembered for next time
        item.Touch();
        await items.UpdateAsync([item], ct);
        return null;
    }
}
