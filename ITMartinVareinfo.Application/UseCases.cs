using ITMartinVareinfo.Domain;

namespace ITMartinVareinfo.Application;

/// <summary>Finding out what things are: a photo of a pile (ONE AI call reads every cover), barcodes, collector texts -
/// and handing it to Lager's box.</summary>
public sealed class ReadingUseCases(
    ICoverReader reader, IPhotoStore photos, IPhotoRepository photoRows, IBarcodeLookup barcodes, ILagerGateway lager,
    VareinfoOptions options, TimeProvider clock)
{
    public const int MaxInterestPerCall = 40;

    public bool AutoInterest => options.AutoInterest;
    public int MaxCallsPerDay => reader.MaxCallsPerDay;
    public Task<int> CallsTodayAsync(CancellationToken ct = default) => reader.CallsTodayAsync(ct);

    /// <summary>Shrinks and keeps the photo (so a wrong reading can be checked against it), then reads it.</summary>
    public async Task<(int PhotoId, List<Found> Found)> ReadPileAsync(Stream photo, string hint, int? boxId, string label, CancellationToken ct = default)
    {
        // Read once into memory: the shrunk copy is kept, the close-ups come from the full-resolution original
        // (user 2026-10-12: titles were poor with ~10 books in one photo - each title was only a few pixels high).
        using var original = new MemoryStream();
        await photo.CopyToAsync(original, ct);
        original.Position = 0;
        var jpeg = await photos.ShrinkAsync(original, ct);
        original.Position = 0;
        var closeUps = await photos.TilesAsync(original, ct);
        var now = clock.GetLocalNow();
        var path = Path.Combine("photos", now.ToString("yyyy-MM"), $"{now:ddHHmmssfff}-{Safe(label)}.jpg");
        await photos.SaveAsync(path, jpeg, ct);
        var found = await reader.ReadPileAsync(jpeg, closeUps, hint, ct);
        var row = await photoRows.AddAsync(new Photo { Path = path, BoxId = boxId, Found = found.Count }, ct);
        return (row.Id, found);
    }

    public Task<(int Added, int Merged)> SaveAsync(int? photoId, IReadOnlyList<Found> found, CancellationToken ct = default) =>
        lager.SaveAsync(photoId, found, ct);

    /// <summary>"Har vi allerede den?" - so a re-photographed pile does not double the stock (user 2026-10-12: ask per item).</summary>
    public Task<List<InStock>> InStockAsync(IReadOnlyList<Found> found, CancellationToken ct = default) =>
        found.Count == 0 ? Task.FromResult(new List<InStock>()) : lager.InStockAsync(found, ct);

    /// <summary>"Det interessante" for up to 40 items in ONE call. Index -> text.</summary>
    public Task<Dictionary<int, string>> WriteInterestAsync(IReadOnlyList<string> lines, CancellationToken ct = default) =>
        reader.WriteInterestAsync(lines.Take(MaxInterestPerCall).ToList(), ct);

    public async Task<string?> PhotoFileAsync(int photoId, CancellationToken ct = default) =>
        await photoRows.GetAsync(photoId, ct) is { } p ? photos.FullPath(p.Path) : null;

    public Task<Found?> LookupBarcodeAsync(string code, CancellationToken ct = default) => barcodes.LookupAsync(Barcodes.Clean(code), ct);

    private static string Safe(string label) => new(label.Where(char.IsLetterOrDigit).Take(20).ToArray());
}

/// <summary>Where Lager is, for links a person clicks.</summary>
public sealed class LagerLinks(ILagerGateway lager)
{
    public string LagerBase => lager.PublicBase;
}

/// <summary>Kolofons (2026-10-11, user: "to save in db and get price just cover. A job can run through kolofon if
/// needed"): Lager says which books need one, the photo is kept here, and the waiting ones are read 10 per AI call and
/// sent to Lager.</summary>
public sealed class KolofonUseCases(IKolofonShotRepository shots, IPhotoStore photos, ICoverReader reader, ILagerGateway lager, TimeProvider clock)
{
    public Task<(int FromKr, List<KolofonWanted> Books)> WantedAsync(CancellationToken ct = default) => lager.KolofonWantedAsync(ct);

    public Task<int> WaitingAsync(CancellationToken ct = default) => shots.WaitingCountAsync(ct);

    public Task<List<KolofonShot>> RecentlyReadAsync(CancellationToken ct = default) => shots.RecentlyReadAsync(20, ct);

    public async Task SavePhotoAsync(int itemId, string title, Stream photo, CancellationToken ct = default)
    {
        var jpeg = await photos.ShrinkAsync(photo, ct);
        var now = clock.GetLocalNow();
        var path = Path.Combine("photos", "kolofon", $"{itemId}-{now:yyyyMMddHHmmss}.jpg");
        await photos.SaveAsync(path, jpeg, ct);
        await shots.AddAsync(new KolofonShot { ItemId = itemId, Title = title, Path = path }, ct);
        await lager.KolofonTakenAsync(itemId, ct);
    }

    public async Task<string?> PhotoFileAsync(int itemId, CancellationToken ct = default) =>
        await shots.LatestForAsync(itemId, ct) is { } s ? photos.FullPath(s.Path) : null;

    /// <summary>Reads up to 10 waiting kolofons in ONE call and sends what was read to Lager. Returns how many.</summary>
    public async Task<int> ReadWaitingAsync(CancellationToken ct = default)
    {
        if (!reader.Enabled) return 0;
        var waiting = await shots.WaitingAsync(KolofonShot.PerCall, ct);
        if (waiting.Count == 0) return 0;
        var withFile = new List<(KolofonShot Shot, byte[] Jpeg)>();
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var s in waiting)
            if (await photos.ReadAsync(s.Path, ct) is { } jpeg) withFile.Add((s, jpeg));
            else s.Read(null, now);   // the file is gone
        var read = withFile.Count > 0 ? await reader.ReadKolofonsAsync(withFile.Select(x => x.Jpeg).ToList(), ct) : [];
        for (var n = 0; n < withFile.Count; n++)
        {
            var r = read.GetValueOrDefault(n);
            await lager.KolofonReadAsync(withFile[n].Shot.ItemId, r, ct);
            withFile[n].Shot.Read(r, now);
        }
        await shots.UpdateAsync(waiting, ct);
        return waiting.Count;
    }
}
