using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>A wrong cut-out made right by a person (user 2026-11-16: "the picture is wrong" - the reader's box around a DVD
/// spine was loose, so the cut-out showed a strip of several DVDs). The person sees the whole pile photo it was read from,
/// drags a box around the thing, and that becomes its photo. The photo before is kept ({id}-original.jpg, the same as for a
/// painted-over price mark), so ItemUseCases.UncoverAsync brings it back.</summary>
public sealed class ItemPhotoUseCases(IItemRepository items, IPhotoStore photos, IVareinfoGateway? vareinfo = null)
{
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
        item.Touch();
        await items.UpdateAsync([item], ct);
        return null;
    }
}
