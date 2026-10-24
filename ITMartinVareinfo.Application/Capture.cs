using ITMartinVareinfo.Domain;

namespace ITMartinVareinfo.Application;

/// <summary>Taking photos box by box (user 2026-10-20: "select the relevant box number ... 100 people on 100 boxes
/// with own phone each. Done in one box time"). The helper types the number on the box; one helper per box at a time.
/// An upload is only kept (fast) - the reading copy and close-ups are made in the background, a few at a time, so 100
/// phones uploading at once never wait on each other.</summary>
public sealed class CaptureUseCases(IBoxClaimRepository claims, IPhotoRepository photoRows, IScanRepository scans, IPhotoStore photos,
    TimeProvider clock)
{
    // Taking a box is check-then-save; one Vareinfo container, so one gate is enough to stop two helpers taking the
    // same box in the same instant.
    private static readonly SemaphoreSlim ClaimGate = new(1, 1);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>"Vælg kasse": the number written on the box. Refused while another helper is filling it.</summary>
    public async Task<BoxClaim> TakeBoxAsync(int number, Taker me, CancellationToken ct = default)
    {
        if (number is < 1 or > BoxClaim.MaxNumber) throw new VareinfoException($"Skriv nummeret på kassen (1-{BoxClaim.MaxNumber}).");
        var code = BoxCodes.For(number);
        await ClaimGate.WaitAsync(ct);
        try
        {
            var claim = await claims.GetAsync(code, ct);
            if (claim is not null && claim.BusyFor(me.Id, Now))
                throw new VareinfoException($"Kasse {number} bruges af {claim.HelperName} lige nu – tag en anden kasse.");
            claim ??= new BoxClaim { BoxCode = code };
            claim.Take(me, Now);
            await claims.SaveAsync(claim, ct);
            return claim;
        }
        finally { ClaimGate.Release(); }
    }

    /// <summary>The box the helper is filling (after a reload or on another day), or null.</summary>
    public async Task<BoxClaim?> MyBoxAsync(Taker me, CancellationToken ct = default) =>
        (await claims.OpenForAsync(me.Id, ct)).FirstOrDefault(c => c.OpenFor(me.Id, Now));

    /// <summary>"Kassen er færdig" - the box is free, the helper picks the next.</summary>
    public async Task FinishBoxAsync(string boxCode, Taker me, CancellationToken ct = default)
    {
        if (await claims.GetAsync(boxCode, ct) is not { } claim || claim.HelperId != me.Id) return;
        claim.Finish(Now);
        await claims.SaveAsync(claim, ct);
    }

    /// <summary>Keeps the upload. Never refused for the box: a photo still waiting on the phone belongs to the box it
    /// was taken in, even if that box was finished or taken over meanwhile.</summary>
    public async Task StoreAsync(Stream photo, string boxCode, Taker me, CancellationToken ct = default)
    {
        var code = CheckedCode(boxCode);
        var now = Now;
        var raw = Path.Combine("inbox", $"{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.jpg");
        await photos.SaveRawAsync(raw, photo, ct);
        await photoRows.AddAsync(new Photo { BoxCode = code, HelperId = me.Id, Helper = me.Name, RawPath = raw, TakenAt = now }, ct);
        await TouchAsync(code, me, ct);
    }

    /// <summary>A barcode into the box - just stored ("beep beep beep"). Returns how many copies of it the box now has.</summary>
    public async Task<int> ScanAsync(string code, string boxCode, Taker me, CancellationToken ct = default)
    {
        var clean = Barcodes.Clean(code);
        if (clean.Length < 8) throw new VareinfoException("Stregkoden kunne ikke læses - prøv igen.");
        var copies = await scans.AddOrCountAsync(clean, CheckedCode(boxCode), me, ct);
        await TouchAsync(boxCode, me, ct);
        return copies;
    }

    /// <summary>Photos and scans in one box so far.</summary>
    public async Task<(int Photos, int Scans)> CountAsync(string boxCode, CancellationToken ct = default) =>
        (await photoRows.CountAsync(boxCode, ct), await scans.CountAsync(boxCode, ct));

    /// <summary>Every box: who has it and how far (Martin's overview).</summary>
    public async Task<List<BoxStatus>> OverviewAsync(CancellationToken ct = default)
    {
        var all = (await claims.AllAsync(ct)).ToDictionary(c => c.BoxCode);
        var photoCounts = await photoRows.PerBoxAsync(ct);
        var scanCounts = await scans.PerBoxAsync(ct);
        var now = Now;
        return all.Keys.Union(photoCounts.Keys).Union(scanCounts.Keys).Select(code =>
            {
                var c = all.GetValueOrDefault(code);
                return new BoxStatus(code, c?.HelperName ?? "", photoCounts.GetValueOrDefault(code), scanCounts.GetValueOrDefault(code),
                    c?.LastAt, c?.DoneAt is not null, c is not null && c.BusyFor(-1, now));
            })
            .OrderByDescending(b => b.Busy).ThenBy(b => b.BoxCode).ToList();
    }

    /// <summary>Makes up to max uploads ready (reading copy + close-ups), parallel at most at a time. The upload is
    /// deleted once the copies are saved. Returns how many were done.</summary>
    public async Task<int> ProcessWaitingAsync(int max, int parallel, CancellationToken ct = default)
    {
        var waiting = await photoRows.UnprocessedAsync(max, ct);
        await Parallel.ForEachAsync(waiting, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel), CancellationToken = ct },
            async (p, token) => await ProcessAsync(p, token));
        return waiting.Count;
    }

    private async Task ProcessAsync(Photo p, CancellationToken ct)
    {
        try
        {
            if (await photos.ReadAsync(p.RawPath, ct) is not { } original)
            {
                p.Attempts = Photo.MaxAttempts - 1;   // the upload is gone - give up at once
                p.Failed();
            }
            else
            {
                var jpeg = await photos.ShrinkAsync(new MemoryStream(original), ct);
                var tiles = await photos.TilesAsync(new MemoryStream(original), ct);
                var path = Path.Combine("photos", p.TakenAt.ToString("yyyy-MM"), $"{p.Id}-{p.BoxCode}.jpg");
                await photos.SaveAsync(path, jpeg, ct);
                for (var n = 0; n < tiles.Count; n++) await photos.SaveAsync(Photo.TilePath(path, n), tiles[n], ct);
                var raw = p.RawPath;
                p.MadeReady(path, tiles.Count);
                await photoRows.UpdateAsync(p, ct);
                photos.Delete(raw);
                return;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { p.Failed(); }
        if (p.Ready && p.RawPath != "") { photos.Delete(p.RawPath); p.RawPath = ""; }
        await photoRows.UpdateAsync(p, ct);
    }

    // ---- for Lager (GET /api/lager/nye, POST /api/lager/modtaget, GET /api/lager/foto/{id}) ----

    public async Task<CaptureBatch> NewForLagerAsync(int max, CancellationToken ct = default) =>
        new((await photoRows.UnsentAsync(max, ct)).Select(p => p.ToCaptured()).ToList(),
            (await scans.UnsentAsync(max, ct)).Select(s => s.ToCaptured()).ToList());

    public async Task ReceivedAsync(CaptureReceived received, CancellationToken ct = default)
    {
        await photoRows.MarkSentAsync(received.PhotoIds ?? [], Now, ct);
        await scans.MarkSentAsync(received.ScanIds ?? [], Now, ct);
    }

    /// <summary>A ready photo's reading copy, or one of its close-ups. Null = not there.</summary>
    public async Task<string?> PhotoFileAsync(int id, int? tile, CancellationToken ct = default)
    {
        if (await photoRows.GetAsync(id, ct) is not { Ready: true, Path.Length: > 0 } p) return null;
        if (tile is { } t) return t >= 0 && t < p.Tiles ? photos.FullPath(Photo.TilePath(p.Path, t)) : null;
        return photos.FullPath(p.Path);
    }

    private static string CheckedCode(string boxCode)
    {
        var n = BoxCodes.Number(boxCode ?? "");
        if (n is < 1 or > BoxClaim.MaxNumber) throw new VareinfoException("Kassen mangler - vælg kassen igen.");
        return BoxCodes.For(n);
    }

    // The box was used just now - it stays the helper's (not idle) as long as photos keep coming.
    private async Task TouchAsync(string boxCode, Taker me, CancellationToken ct)
    {
        if (await claims.GetAsync(CheckedCode(boxCode), ct) is { } claim && claim.HelperId == me.Id && claim.DoneAt is null)
        {
            claim.LastAt = Now;
            await claims.SaveAsync(claim, ct);
        }
    }
}

/// <summary>The helpers and their codes (Martin's Hjælpere page), and signing in with a code.</summary>
/// <summary>Kunder (user 2026-10-24): is this kundenummer a helper Martin has ticked? Name = who it is; null = no.</summary>
public interface IHelperNumbers
{
    Task<string?> HelperNameAsync(string kundenummer, CancellationToken ct = default);
}

public sealed class HelperUseCases(IHelperRepository helpers, VareinfoOptions options, IHelperNumbers? numbers = null)
{
    public string LagerBase => options.LagerPublicUrl.TrimEnd('/');

    /// <summary>The main PIN = Martin (Id 0); otherwise a helper's own code. Null = no one.</summary>
    public async Task<Taker?> SignInAsync(string? code, CancellationToken ct = default)
    {
        code = (code ?? "").Trim();
        if (code.Length < Helper.CodeLength) return null;
        if (options.AdminPin.Length >= 6 && code == options.AdminPin) return new Taker(0, options.AdminName);
        if (await helpers.ByCodeAsync(code, ct) is { Active: true } h) return h.AsTaker;
        // A helper's kundenummer (8 digits, spaces allowed): Id = minus the number, so it never meets a code helper's Id.
        var digits = new string(code.Where(char.IsDigit).ToArray());
        return digits.Length == 8 && numbers is not null && await numbers.HelperNameAsync(digits, ct) is { } name
            ? new Taker(-int.Parse(digits), name) : null;
    }

    public Task<List<Helper>> AllAsync(CancellationToken ct = default) => helpers.ActiveAsync(ct);

    /// <summary>A new helper with a new six-digit code (never Martin's PIN, never another helper's).</summary>
    public async Task<Helper> AddAsync(string name, CancellationToken ct = default)
    {
        name = name.Trim();
        if (name.Length == 0) throw new VareinfoException("Skriv hjælperens navn.");
        string code;
        do code = Helper.NewCode(Random.Shared);
        while (code == options.AdminPin || await helpers.ByCodeAsync(code, ct) is not null);
        var helper = new Helper { Name = name, Code = code };
        await helpers.AddAsync(helper, ct);
        return helper;
    }

    /// <summary>The code stops working at once; the photos the helper took are kept.</summary>
    public async Task RemoveAsync(int id, CancellationToken ct = default)
    {
        if (await helpers.GetAsync(id, ct) is not { } h) return;
        h.Active = false;
        await helpers.UpdateAsync(h, ct);
    }
}
