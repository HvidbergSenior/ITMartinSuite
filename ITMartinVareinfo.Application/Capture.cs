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

    /// <summary>The iPhone Shortcut "Bogshoppen foto" (user 2026-11-15: "a small app that take pictures with my own cam and send
    /// immediately"): one JPEG straight into today's session - nothing to choose. HEIC is refused here with words, because the
    /// background job could not open it later (the Shortcut converts to JPEG first). Returns today's session title.</summary>
    public async Task<string> StoreTodayAsync(byte[] jpeg, Taker me, CancellationToken ct = default)
    {
        if (!IsJpeg(jpeg)) throw new VareinfoException("Billedet er ikke JPEG - sæt »Konverter billede til JPEG« ind i genvejen før »Hent indhold af URL«.");
        var today = await sessions.TodayAsync(me, CaptureKinds.Mixed, ct);
        await StoreAsync(new MemoryStream(jpeg), today.Id, me, ct);
        return today.Title;
    }

    /// <summary>JPEG starts with FF D8 FF.</summary>
    public static bool IsJpeg(byte[] b) => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    /// <summary>Photos and scans in one session so far.</summary>
    /// <summary>Photos in the session, and how many of them Lager already has ("færdig" on the page, user 2026-11-15).</summary>
    public async Task<(int Photos, int AtLager)> CountAsync(int sessionId, CancellationToken ct = default) =>
        (await photoRows.CountAsync(sessionId, ct), await photoRows.CountAtLagerAsync(sessionId, ct));

    /// <summary>Deletes the close-ups of photos Lager has had for a week - the reading copy stays (bogshoppen.dk can show it).
    /// Returns how many photos were cleaned.</summary>
    public async Task<int> CleanOldTilesAsync(int max, CancellationToken ct = default)
    {
        var old = await photoRows.WithOldTilesAsync(Now - Photo.TilesKeptFor, max, ct);
        foreach (var p in old)
        {
            for (var n = 0; n < p.Tiles; n++) photos.Delete(Photo.TilePath(p.Path, n));
            p.Tiles = 0;
            await photoRows.UpdateAsync(p, ct);
        }
        return old.Count;
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
                var path = Path.Combine("photos", p.TakenAt.ToString("yyyy-MM"), $"{p.Id}-s{p.SessionId}.jpg");
                await photos.SaveAsync(path, jpeg, ct);
                for (var n = 0; n < tiles.Count; n++) await photos.SaveAsync(Photo.TilePath(path, n), tiles[n], ct);
                var raw = p.RawPath;
                p.MadeReady(path, tiles.Count);
                await photoRows.UpdateAsync(p, ct);
                photos.Move(raw, p.NasPath);   // the original goes to the NAS (sync script), not deleted
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

/// <summary>Kunder (2026-10-26): is this kundenummer an admin (Kunder's Admin tick)? Name = who it is; null = no.</summary>
public interface IHelperNumbers
{
    Task<string?> AdminNameAsync(string kundenummer, CancellationToken ct = default);
}

/// <summary>Signing in - only Martin (user 2026-11-10: "i also think the hjælpere feature should be deleted" -> only Martin): the main PIN, or a kundenummer with Kunder's Admin tick.</summary>
public sealed class HelperUseCases(VareinfoOptions options, IHelperNumbers? numbers = null)
{
    public string LagerBase => options.LagerPublicUrl.TrimEnd('/');

    /// <summary>The main PIN or an admin's kundenummer = Martin (Id 0). Null = no one.</summary>
    public async Task<Taker?> SignInAsync(string? code, CancellationToken ct = default)
    {
        code = (code ?? "").Trim();
        if (options.AdminPin.Length >= 6 && code == options.AdminPin) return new Taker(0, options.AdminName);
        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length != 8 || numbers is null) return null;
        return await numbers.AdminNameAsync(digits, ct) is { } admin ? new Taker(0, admin) : null;
    }
}
