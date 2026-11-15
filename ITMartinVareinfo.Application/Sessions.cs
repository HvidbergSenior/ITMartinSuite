using ITMartinVareinfo.Domain;

namespace ITMartinVareinfo.Application;

/// <summary>The day sessions (user 2026-11-10: "automatic session on days"; 2026-11-15: "remove sessioner from this") - one per
/// day per button, made by the button, closed by the next day. Nobody starts, names or closes one by hand.</summary>
public interface ISessionRepository
{
    /// <summary>Newest first, practice sessions included.</summary>
    Task<List<WorkSession>> AllAsync(CancellationToken ct);
    Task<WorkSession?> GetAsync(int id, CancellationToken ct);
    /// <summary>Adds (Id 0) or updates.</summary>
    Task SaveAsync(WorkSession session, CancellationToken ct);
}

public sealed class SessionUseCases(ISessionRepository sessions, IPhotoRepository photos, IScanRepository scans, TimeProvider clock)
{
    // Numbering and practice sessions are check-then-save; one Vareinfo container, so one gate is enough.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>The real sessions (not the practice ones), newest first.</summary>
    public async Task<List<WorkSession>> AllAsync(CancellationToken ct = default) =>
        (await sessions.AllAsync(ct)).Where(s => !s.IsPractice).ToList();

    /// <summary>One press on the start screen (user 2026-11-10: "maybe automatic session on days"): today's session of that
    /// button ("Blade og album") - joined when it is there, made when it is not. Day sessions from earlier days are closed on
    /// the way (photos still on a phone still come in). Tjek is its own app now (2026-11-15), so these are always Mine varer.</summary>
    public async Task<WorkSession> TodayAsync(Taker me, string? kind = null, CancellationToken ct = default)
    {
        kind = CaptureKinds.IsKind(kind) ? kind! : CaptureKinds.Mixed;
        await Gate.WaitAsync(ct);
        try
        {
            var all = await AllAsync(ct);
            await CloseOldDaysAsync(all, ct);
            if (all.Where(s => s.IsToday(kind, Now) && s.IsFor(me)).OrderByDescending(s => s.CreatedAt).FirstOrDefault() is { } today) return today;
            var s = Guard(() => WorkSession.Start(WorkSession.NextNumber(all.Select(x => x.Number)), WorkSession.TodayName(kind, Now), Now, kind));
            await sessions.SaveAsync(s, ct);
            return s;
        }
        finally { Gate.Release(); }
    }

    // Day sessions close by themselves when the day is over.
    private async Task CloseOldDaysAsync(IEnumerable<WorkSession> all, CancellationToken ct)
    {
        foreach (var old in all.Where(s => s.IsOldDaily(Now)).ToList())
        {
            old.Close(Now);
            await sessions.SaveAsync(old, ct);
        }
    }

    /// <summary>The session that is open on the page (its id is in the address), when it is still open.</summary>
    public async Task<WorkSession?> OpenForAsync(int id, Taker me, CancellationToken ct = default) =>
        await sessions.GetAsync(id, ct) is { Open: true } s && s.IsFor(me) ? s : null;

    /// <summary>Martin's overview: every session with its photos, scans and helpers - open ones first.</summary>
    public async Task<List<SessionStatus>> OverviewAsync(CancellationToken ct = default)
    {
        var p = await photos.PerSessionAsync(ct);
        var c = await scans.PerSessionAsync(ct);
        return (await AllAsync(ct)).Select(s => SessionStatus.For(s, p, c))
            .OrderByDescending(s => s.Session.Open).ThenByDescending(s => s.Session.Number).ToList();
    }

    /// <summary>The session title and whether it is a Fund session, for these Lager codes.</summary>
    public async Task<List<CapturedBox>> BoxesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default) =>
        (await sessions.AllAsync(ct)).Where(s => !s.IsPractice && codes.Contains(s.BoxCode))
            .GroupBy(s => s.BoxCode).Select(g => new CapturedBox(g.Key, g.First().Title, g.First().Fund, g.First().Kind, g.First().IsDaily)).ToList();

    private static T Guard<T>(Func<T> rule)
    {
        try { return rule(); }
        catch (InvalidOperationException ex) { throw new VareinfoException(ex.Message); }
    }
}
