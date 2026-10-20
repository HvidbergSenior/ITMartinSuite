using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>Kolofons (2026-10-11, user: "to save in db and get price just cover. A job can run through kolofon if
/// needed"): the list says which books need one, the photo is taken on the item page here, and the job reads the
/// waiting ones 10 per AI call (2026-10-20: moved here from Vareinfo, which now only takes the box photos).</summary>
public sealed class KolofonUseCases(IItemRepository items, IPhotoStore photos, ICoverReader reader, LagerOptions options, TimeProvider clock)
{
    public int FromKr => options.KolofonFromKr;

    public Task<List<(Item Item, int TopKr)>> WantedAsync(CancellationToken ct = default) => items.KolofonWantedAsync(FromKr, 200, ct);

    public Task<List<Item>> WaitingAsync(CancellationToken ct = default) => items.KolofonWaitingAsync(200, ct);

    /// <summary>Keeps the photo; the book leaves the list and shows "venter på at blive læst". Returns its path.</summary>
    public async Task<string> SavePhotoAsync(int itemId, Stream photo, CancellationToken ct = default)
    {
        var item = await items.GetAsync(itemId, ct) ?? throw new LagerException("Varen findes ikke længere.");
        var jpeg = await photos.ShrinkAsync(photo, ct);
        var path = Path.Combine("photos", "kolofon", $"{itemId}-{clock.GetLocalNow():yyyyMMddHHmmss}.jpg");
        await photos.SaveAsync(path, jpeg, ct);
        Kolofon.PhotoTaken(item, path);
        await items.UpdateAsync([item], ct);
        return path;
    }

    public async Task<string?> PhotoFileAsync(int itemId, CancellationToken ct = default) =>
        await items.GetAsync(itemId, ct) is { } i ? photos.FullPath(i.KolofonPhoto) : null;

    /// <summary>Reads up to 10 waiting kolofons in ONE call and puts what was read on the books. Returns how many.
    /// A new ISBN makes the price hunter search again.</summary>
    public async Task<int> ReadWaitingAsync(CancellationToken ct = default)
    {
        if (!reader.Enabled) return 0;
        var waiting = await items.KolofonWaitingAsync(Kolofon.PerCall, ct);
        if (waiting.Count == 0) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var withFile = new List<(Item Item, byte[] Jpeg)>();
        foreach (var item in waiting)
            if (await photos.ReadAsync(item.KolofonPhoto, ct) is { } jpeg) withFile.Add((item, jpeg));
            else Kolofon.Unreadable(item, now);   // the file is gone
        var read = withFile.Count > 0 ? await reader.ReadKolofonsAsync(withFile.Select(x => x.Jpeg).ToList(), ct) : [];
        for (var n = 0; n < withFile.Count; n++)
            if (read.GetValueOrDefault(n) is { } r) Kolofon.Apply(withFile[n].Item, r, now);
            else Kolofon.Unreadable(withFile[n].Item, now);
        await items.UpdateAsync(waiting, ct);
        return waiting.Count;
    }
}
