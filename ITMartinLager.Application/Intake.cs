using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>From Vareinfo's boxes into Lager's (2026-10-20, user: "One app for just taking pictures and storing"):
/// the job fetches the helpers' new photos and scans, and makes each box the first time it is seen (the number is the one
/// written on the box). Nothing is read until the person presses Læs: then one AI call per pile photo, under the daily
/// cap, and the barcodes are looked up on the free sites. What was found goes into its box.</summary>
public sealed class IntakeUseCases(IVareinfoGateway vareinfo, IIntakeRepository intake, IBoxRepository boxes,
    RegistrationUseCases registration, ICoverReader reader, IBarcodeLookup barcodes, TimeProvider clock)
{
    /// <summary>Photos and scans fetched per round - the job runs every minute.</summary>
    public const int FetchPerRound = 200;
    /// <summary>Pile photos read per round (one AI call each).</summary>
    public const int ReadPerRound = 5;
    /// <summary>Barcodes looked up per round (free sites - they should not be hammered).</summary>
    public const int ScansPerRound = 20;

    public int MaxCallsPerDay => reader.MaxCallsPerDay;
    public Task<int> CallsTodayAsync(CancellationToken ct = default) => reader.CallsTodayAsync(ct);
    public string VareinfoBase => vareinfo.PublicBase;

    /// <summary>Takes over what is new in Vareinfo. Vareinfo is told only after it is kept here, so nothing is lost
    /// when either side is down. Returns how many photos and scans came.</summary>
    public async Task<int> FetchAsync(CancellationToken ct = default)
    {
        var batch = await vareinfo.NewAsync(FetchPerRound, ct);
        if (batch.Photos.Count + batch.Scans.Count == 0) return 0;
        var owners = batch.Photos.Select(p => (p.BoxCode, p.Helper)).Concat(batch.Scans.Select(s => (s.BoxCode, s.Helper)))
            .GroupBy(x => x.BoxCode).ToDictionary(g => g.Key, g => g.First().Helper);
        foreach (var (code, owner) in owners)
        {
            var box = await boxes.GetOrAddAsync(code, ct);
            // A personal test box (9000-9999) is named after its owner the first time, so Kasser shows whose it is.
            if (BoxCodes.IsTest(code) && box.Place == "") await boxes.SetPlaceAsync(box.Id, $"Test {owner}".Trim(), ct);
        }
        await intake.AddAsync(
            batch.Photos.Select(p => new BoxShot { VareinfoId = p.Id, BoxCode = p.BoxCode, Helper = p.Helper, Tiles = p.Tiles, TakenAt = p.TakenAt }),
            batch.Scans.Select(s => new BoxScan { VareinfoId = s.Id, BoxCode = s.BoxCode, Helper = s.Helper, Code = s.Code, Quantity = s.Quantity, TakenAt = s.TakenAt }),
            ct);
        await vareinfo.ReceivedAsync(new CaptureReceived(batch.Photos.Select(p => p.Id).ToList(), batch.Scans.Select(s => s.Id).ToList()), ct);
        return batch.Photos.Count + batch.Scans.Count;
    }

    public Task<List<BoxIntake>> BoxesAsync(CancellationToken ct = default) => intake.PerBoxAsync(ct);

    /// <summary>"Læs" - one box, or every box (null). The job does the reading.</summary>
    public Task<int> WantReadAsync(string? boxCode, CancellationToken ct = default) => intake.WantReadAsync(boxCode, ct);

    /// <summary>Looks up the wanted scans and reads up to ReadPerRound wanted photos into their boxes. A photo is marked
    /// read only after its things are in the box. Stops at the daily cap (the reader throws).</summary>
    public async Task<int> ReadWantedAsync(CancellationToken ct = default)
    {
        var done = await LookUpScansAsync(ct);
        if (!reader.Enabled) return done;
        foreach (var shot in await intake.WantedShotsAsync(ReadPerRound, ct))
        {
            var now = clock.GetUtcNow().UtcDateTime;
            if (await vareinfo.PhotoAsync(shot.VareinfoId, null, ct) is not { } jpeg)
            {
                shot.Read(0, now);   // the photo is gone in Vareinfo
                await intake.UpdateAsync([shot], ct);
                continue;
            }
            var closeUps = new List<byte[]>();
            for (var n = 0; n < shot.Tiles; n++)
                if (await vareinfo.PhotoAsync(shot.VareinfoId, n, ct) is { } t) closeUps.Add(t);
            var found = await reader.ReadPileAsync(jpeg, closeUps, "", ct);
            if (found.Count > 0) await SaveInBoxAsync(shot.BoxCode, shot.VareinfoId, found, ct);
            shot.Read(found.Count, now);
            await intake.UpdateAsync([shot], ct);
            done++;
        }
        return done;
    }

    // A code no source knows is NOT saved as an item (user 2026-10-21): it is usually a Danish DVD/CD whose cover photo
    // already found it, so an "Ukendt" item would count it twice. The scan stays (Known = false) and Kasser shows the count.
    // Each box's things go in at once.
    private async Task<int> LookUpScansAsync(CancellationToken ct)
    {
        var wanted = await intake.WantedScansAsync(ScansPerRound, ct);
        foreach (var box in wanted.GroupBy(s => s.BoxCode))
        {
            var found = new List<Found>();
            var known = new Dictionary<BoxScan, bool>();
            foreach (var s in box)
            {
                var f = await barcodes.LookupAsync(s.Code, ct);
                known[s] = f is not null;
                if (f is not null) found.Add(f with { Barcode = s.Code, Quantity = s.Quantity });
            }
            await SaveInBoxAsync(box.Key, null, found, ct);
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var s in box) s.Read(known[s], now);
            await intake.UpdateAsync(box, ct);
        }
        return wanted.Count;
    }

    private async Task SaveInBoxAsync(string boxCode, int? photoId, List<Found> found, CancellationToken ct)
    {
        var box = await boxes.GetOrAddAsync(boxCode, ct);
        var clean = found.Where(f => !string.IsNullOrWhiteSpace(f.Title))
            .Select(f => f with { Kind = Kinds.Normalise(f.Kind), Condition = Conditions.Normalise(f.Condition), Quantity = Math.Max(1, f.Quantity) })
            .ToList();
        await registration.SaveAsync(box.Id, photoId, clean, ct);
    }
}

/// <summary>Where Vareinfo is, for the buttons and photo links a person clicks.</summary>
public sealed class VareinfoLinks(IVareinfoGateway vareinfo)
{
    public string Base => vareinfo.PublicBase;
    public string Photo(int photoId) => $"{vareinfo.PublicBase}/foto/{photoId}";
}
