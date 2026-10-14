using ITMartinVareinfo.Domain;

namespace ITMartinVareinfo.Application;

/// <summary>Box by box (user 2026-10-15): "I take one box at a time. Write NR 1, 2, 3 ... Just take pictures of all
/// products in one box ... Take picture -> storage -> when time comes I will look at prices". A photo is only stored
/// here (with its close-ups); when the person presses Læs, the reader job reads it - one AI call per pile photo, under
/// the daily cap - and puts what it found in that box on Lager. No review step: Lager is where it is checked and priced.</summary>
public sealed class BoxPhotoUseCases(IPhotoRepository photoRows, IPhotoStore photos, ICoverReader reader, ILagerGateway lager, TimeProvider clock)
{
    /// <summary>Pile photos read per job round - the job runs every minute.</summary>
    public const int PerRound = 5;

    /// <summary>The box being filled = the newest box on Lager. The very first one is made when there is none.</summary>
    public async Task<LagerBox> CurrentBoxAsync(CancellationToken ct = default) =>
        await lager.LatestBoxAsync(ct) ?? await lager.NewBoxAsync(ct);

    /// <summary>"Kassen er fuld" - the next number. Refused while the current box has no photos, so a double tap
    /// never skips a number.</summary>
    public async Task<LagerBox> NewBoxAsync(CancellationToken ct = default)
    {
        var current = await CurrentBoxAsync(ct);
        var boxes = await photoRows.BoxesAsync(ct);
        if (!boxes.Any(b => b.BoxCode == current.Code))
            throw new VareinfoException($"Kasse {current.Number} har ingen billeder endnu - tag billederne af den først.");
        return await lager.NewBoxAsync(ct);
    }

    /// <summary>Keeps the photo (shrunk) and its close-ups from the full-size original - nothing is read now.</summary>
    public async Task<Photo> StoreAsync(Stream photo, LagerBox box, CancellationToken ct = default)
    {
        using var original = new MemoryStream();
        await photo.CopyToAsync(original, ct);
        original.Position = 0;
        var jpeg = await photos.ShrinkAsync(original, ct);
        original.Position = 0;
        var tiles = await photos.TilesAsync(original, ct);
        var now = clock.GetLocalNow();
        var path = Path.Combine("photos", now.ToString("yyyy-MM"), $"{now:ddHHmmssfff}-{box.Code}.jpg");
        await photos.SaveAsync(path, jpeg, ct);
        for (var n = 0; n < tiles.Count; n++) await photos.SaveAsync(Photo.TilePath(path, n), tiles[n], ct);
        return await photoRows.AddAsync(new Photo { Path = path, BoxId = box.Id, BoxCode = box.Code, Tiles = tiles.Count, Waiting = true }, ct);
    }

    public Task<List<BoxPhotos>> BoxesAsync(CancellationToken ct = default) => photoRows.BoxesAsync(ct);

    /// <summary>"Læs" - one box, or every box (null). The job does the reading.</summary>
    public Task<int> WantReadAsync(string? boxCode, CancellationToken ct = default) => photoRows.WantReadAsync(boxCode, ct);

    /// <summary>Reads up to PerRound wanted photos, one AI call each, and saves what was found in their box on Lager.
    /// A photo is marked read only after Lager has it, so nothing is lost when Lager is down. Stops at the daily cap.</summary>
    public async Task<int> ReadWantedAsync(CancellationToken ct = default)
    {
        if (!reader.Enabled) return 0;
        var done = 0;
        foreach (var p in await photoRows.WantedAsync(PerRound, ct))
        {
            if (await photos.ReadAsync(p.Path, ct) is not { } jpeg)
            {
                p.Read(0);   // the file is gone
                await photoRows.UpdateAsync(p, ct);
                continue;
            }
            var closeUps = new List<byte[]>();
            for (var n = 0; n < p.Tiles; n++)
                if (await photos.ReadAsync(Photo.TilePath(p.Path, n), ct) is { } t) closeUps.Add(t);
            var found = await reader.ReadPileAsync(jpeg, closeUps, "", ct);
            if (found.Count > 0) await lager.SaveInBoxAsync(p.BoxCode, p.Id, found, ct);
            p.Read(found.Count);
            await photoRows.UpdateAsync(p, ct);
            done++;
        }
        return done;
    }
}
