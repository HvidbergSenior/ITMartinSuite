using ITMartinVareinfo.Domain;

namespace ITMartinVareinfo.Application;

/// <summary>Taking photos in a session (user 2026-10-29: "Session 1, 2, 3 ... named something like location and/or
/// intent" - no boxes). Many helpers in the same session at once, each with their own phone. An upload is only kept
/// (fast) - the reading copy and close-ups are made in the background, a few at a time, so 100 phones uploading at once
/// never wait on each other.</summary>
public sealed class CaptureUseCases(IPhotoRepository photoRows, IScanRepository scans, IPhotoStore photos,
    ISessionRepository sessionRows, SessionUseCases sessions, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Keeps the upload. Not refused because the session was closed meanwhile: a photo still waiting on the
    /// phone belongs to the session it was taken in.</summary>
    public async Task StoreAsync(Stream photo, int sessionId, Taker me, CancellationToken ct = default)
    {
        var s = await SessionAsync(sessionId, me, ct);
        var now = Now;
        var raw = Path.Combine("inbox", $"{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.jpg");
        await photos.SaveRawAsync(raw, photo, ct);
        await photoRows.AddAsync(new Photo { SessionId = s.Id, BoxCode = s.BoxCode, HelperId = me.Id, Helper = me.Name, RawPath = raw, TakenAt = now }, ct);
    }

    /// <summary>A barcode in the session - just stored ("beep beep beep"). Returns how many copies of it the session now has.</summary>
    public async Task<int> ScanAsync(string code, int sessionId, Taker me, CancellationToken ct = default)
    {
        var clean = Barcodes.Clean(code);
        if (clean.Length < 8) throw new VareinfoException("Stregkoden kunne ikke læses - prøv igen.");
        return await scans.AddOrCountAsync(clean, await SessionAsync(sessionId, me, ct), me, ct);
    }

    /// <summary>Photos and scans in one session so far.</summary>
    public async Task<(int Photos, int Scans)> CountAsync(int sessionId, CancellationToken ct = default) =>
        (await photoRows.CountAsync(sessionId, ct), await scans.CountAsync(sessionId, ct));

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
                var path = Path.Combine("photos", p.TakenAt.ToString("yyyy-MM"), $"{p.Id}-s{p.SessionId}.jpg");
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

    public async Task<CaptureBatch> NewForLagerAsync(int max, CancellationToken ct = default)
    {
        var batch = new CaptureBatch((await photoRows.UnsentAsync(max, ct)).Select(p => p.ToCaptured()).ToList(),
            (await scans.UnsentAsync(max, ct)).Select(s => s.ToCaptured()).ToList());
        var codes = batch.Photos.Select(p => p.BoxCode).Concat(batch.Scans.Select(s => s.BoxCode)).ToHashSet();
        if (codes.Count == 0) return batch;
        // The session titles go along, so Lager's Kasser says "Session 3 – Boxit, reol 2" (Lager keeps them per code).
        return batch with { Boxes = await sessions.BoxesAsync(codes, ct) };
    }

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

    // The session the photo was taken in - it must be one this person may use (open or closed).
    private async Task<WorkSession> SessionAsync(int sessionId, Taker me, CancellationToken ct) =>
        await sessionRows.GetAsync(sessionId, ct) is { } s && s.IsFor(me) ? s : throw new SessionGoneException();
}

/// <summary>The helpers and their codes (Martin's Hjælpere page), and signing in with a code.</summary>
/// <summary>Kunder (user 2026-10-24): is this kundenummer a helper Martin has ticked? Name = who it is; null = no.</summary>
public interface IHelperNumbers
{
    Task<string?> HelperNameAsync(string kundenummer, CancellationToken ct = default);

    /// <summary>Kunder's Admin tick (2026-10-26): the kundenummer opens the app as admin. Name = who it is; null = no.</summary>
    Task<string?> AdminNameAsync(string kundenummer, CancellationToken ct = default) => Task.FromResult<string?>(null);

    /// <summary>Kunder's ticked helpers (kundenummer, name) - for the helper choice per session. Empty when unknown.</summary>
    Task<List<(string Kundenummer, string Name)>> AllAsync(CancellationToken ct = default) => Task.FromResult(new List<(string, string)>());
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
        if (digits.Length != 8 || numbers is null) return null;
        // An admin's kundenummer (Kunder's Admin tick, 2026-10-26) opens it like the main PIN.
        if (await numbers.AdminNameAsync(digits, ct) is { } admin) return new Taker(0, admin);
        return await numbers.HelperNameAsync(digits, ct) is { } name ? new Taker(-int.Parse(digits), name) : null;
    }

    public Task<List<Helper>> AllAsync(CancellationToken ct = default) => helpers.ActiveAsync(ct);

    /// <summary>Everyone Martin can give a session to: Kunder's ticked helpers and the helpers with their own code here.</summary>
    public async Task<List<HelperChoice>> ChoicesAsync(CancellationToken ct = default)
    {
        var fromKunder = numbers is null ? [] : await numbers.AllAsync(ct);
        return fromKunder.Select(h => new HelperChoice(-int.Parse(h.Kundenummer), h.Name))
            .Concat((await helpers.ActiveAsync(ct)).Select(h => new HelperChoice(h.Id, h.Name)))
            .OrderBy(h => h.Name).ToList();
    }

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
