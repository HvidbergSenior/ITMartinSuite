using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>The kolofon job (2026-10-11, user: "to save in db and get price just cover. A job can run through kolofon
/// if needed"): which books need a kolofon photo, taking it, and reading the waiting ones - 10 per AI call.</summary>
public sealed class KolofonUseCases(IItemRepository items, IPhotoStore photos, ICoverReader reader, LagerOptions options, TimeProvider clock)
{
    public int FromKr => options.KolofonFromKr;

    public Task<List<(Item Item, int TopKr)>> WantedAsync(CancellationToken ct = default) => items.KolofonWantedAsync(FromKr, 200, ct);

    public Task<int> WaitingAsync(CancellationToken ct = default) => items.KolofonWaitingCountAsync(ct);

    public async Task<Item> SavePhotoAsync(int itemId, Stream photo, CancellationToken ct = default)
    {
        var item = await items.GetAsync(itemId, ct) ?? throw new LagerException("Varen findes ikke længere.");
        var jpeg = await photos.ShrinkAsync(photo, ct);
        var path = Path.Combine("photos", "kolofon", $"{itemId}.jpg");
        await photos.SaveAsync(path, jpeg, ct);
        Kolofon.PhotoTaken(item, path);
        await items.UpdateAsync([item], ct);
        return item;
    }

    /// <summary>Reads up to 10 waiting kolofons in ONE call. Returns how many were handled.</summary>
    public async Task<int> ReadWaitingAsync(CancellationToken ct = default)
    {
        if (!reader.Enabled) return 0;
        var waiting = await items.KolofonWaitingAsync(Kolofon.PerCall, ct);
        if (waiting.Count == 0) return 0;
        var withFile = new List<(Item Item, byte[] Jpeg)>();
        foreach (var i in waiting)
        {
            if (await photos.ReadAsync(i.KolofonPhoto, ct) is { } jpeg) withFile.Add((i, jpeg));
            else { i.KolofonPhoto = ""; i.Touch(); }   // the file is gone - it goes back on the list
        }
        var now = clock.GetUtcNow().UtcDateTime;
        if (withFile.Count > 0)
        {
            var read = await reader.ReadKolofonsAsync(withFile.Select(x => x.Jpeg).ToList(), ct);
            for (var n = 0; n < withFile.Count; n++)
                if (read.TryGetValue(n, out var r)) Kolofon.Apply(withFile[n].Item, r, now);
                else Kolofon.Unreadable(withFile[n].Item, now);
        }
        await items.UpdateAsync(waiting, ct);
        return waiting.Count;
    }
}
