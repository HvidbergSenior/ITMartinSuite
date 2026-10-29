using ITMartinVareinfo.Domain;

namespace ITMartinVareinfo.Application;

/// <summary>The scanning sessions (user 2026-10-29: "Session 1, 2, 3, etc... named something like location and/or
/// intent"). Martin starts one; every helper it is for sees it while it is open and taps it.</summary>
public interface ISessionRepository
{
    /// <summary>Newest first, practice sessions included.</summary>
    Task<List<WorkSession>> AllAsync(CancellationToken ct);
    Task<WorkSession?> GetAsync(int id, CancellationToken ct);
    /// <summary>Adds (Id 0) or updates.</summary>
    Task SaveAsync(WorkSession session, CancellationToken ct);
    Task DeleteAsync(int sessionId, CancellationToken ct);
}

/// <summary>Someone Martin can give a session to: Taker.Id (minus the kundenummer for Kunder's helpers) and the name.</summary>
public sealed record HelperChoice(int Id, string Name);

/// <summary>One button on the helper's list.</summary>
public sealed record SessionChoice(int Id, string Title, int Photos);

public sealed class SessionUseCases(ISessionRepository sessions, IPhotoRepository photos, IScanRepository scans, TimeProvider clock)
{
    // Numbering and practice sessions are check-then-save; one Vareinfo container, so one gate is enough.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>The real sessions (not the practice ones), newest first.</summary>
    public async Task<List<WorkSession>> AllAsync(CancellationToken ct = default) =>
        (await sessions.AllAsync(ct)).Where(s => !s.IsPractice).ToList();

    /// <summary>"Ny session": the next number, and the place/purpose Martin wrote (may be empty).</summary>
    public async Task<WorkSession> StartAsync(string? name, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var number = WorkSession.NextNumber((await AllAsync(ct)).Select(s => s.Number));
            var s = Guard(() => WorkSession.Start(number, name, Now));
            await sessions.SaveAsync(s, ct);
            return s;
        }
        finally { Gate.Release(); }
    }

    public Task RenameAsync(int id, string? name, CancellationToken ct = default) => ChangeAsync(id, s => s.Rename(name), ct);

    /// <summary>"Til": the session is for this helper only (null = every helper).</summary>
    public Task GiveAsync(int id, HelperChoice? helper, CancellationToken ct = default) => ChangeAsync(id, s => s.GiveTo(helper?.Id, helper?.Name), ct);

    /// <summary>Closed: it leaves the helpers' list (the photos are kept, and photos still on a phone still come in).</summary>
    public Task CloseAsync(int id, CancellationToken ct = default) => ChangeAsync(id, s => s.Close(Now), ct);

    public Task ReopenAsync(int id, CancellationToken ct = default) => ChangeAsync(id, s => s.ClosedAt = null, ct);

    /// <summary>Only a session nobody took a photo or scan in - one with photos is closed instead, so a product can
    /// always be found again by its session.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        if (await photos.CountAsync(id, ct) + await scans.CountAsync(id, ct) > 0)
            throw new VareinfoException("Der er taget billeder i sessionen - luk den i stedet. Så kan tingene findes igen.");
        await sessions.DeleteAsync(id, ct);
    }

    /// <summary>The helper's list: the open sessions for them. A helper who was given sessions sees only those; the
    /// others see the ones for everybody. Martin sees all.</summary>
    public async Task<List<SessionChoice>> ChoicesAsync(Taker me, CancellationToken ct = default)
    {
        var open = (await AllAsync(ct)).Where(s => s.Open && s.IsFor(me)).OrderBy(s => s.Number).ToList();
        if (!me.IsAdmin && open.Any(s => s.HelperId == me.Id)) open = open.Where(s => s.HelperId == me.Id).ToList();
        if (open.Count == 0) return [];
        var counts = await photos.PerSessionAsync(ct);
        return open.Select(s => new SessionChoice(s.Id, s.Title, counts.Where(t => t.SessionId == s.Id).Sum(t => t.Count))).ToList();
    }

    /// <summary>The session the helper opened (the page keeps its id in the address), when it is open and for them.</summary>
    public async Task<WorkSession?> OpenForAsync(int id, Taker me, CancellationToken ct = default) =>
        await sessions.GetAsync(id, ct) is { Open: true } s && s.IsFor(me) ? s : null;

    /// <summary>"Min øvesession": the person's own - the same one every time, made the first time (codes from 9999 down).</summary>
    public async Task<WorkSession> PracticeAsync(Taker me, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var all = (await sessions.AllAsync(ct)).Where(s => s.IsPractice).ToList();
            if (all.FirstOrDefault(s => s.HelperId == me.Id) is { } mine) return mine;
            var used = all.Select(s => BoxCodes.Number(s.BoxCode)).ToHashSet();
            var free = Enumerable.Range(BoxCodes.FirstTest, 9999 - BoxCodes.FirstTest + 1).Reverse().FirstOrDefault(n => !used.Contains(n));
            if (free == 0) throw new VareinfoException("Der er ikke flere øvesessioner - spørg Martin.");
            var s = WorkSession.Practice(me, free, Now);
            await sessions.SaveAsync(s, ct);
            return s;
        }
        finally { Gate.Release(); }
    }

    /// <summary>Martin's overview: every session with its photos, scans and helpers - open ones first.</summary>
    public async Task<List<SessionStatus>> OverviewAsync(CancellationToken ct = default)
    {
        var p = await photos.PerSessionAsync(ct);
        var c = await scans.PerSessionAsync(ct);
        return (await AllAsync(ct)).Select(s => SessionStatus.For(s, p, c))
            .OrderByDescending(s => s.Session.Open).ThenByDescending(s => s.Session.Number).ToList();
    }

    /// <summary>The session titles for these Lager codes - Lager shows them on Kasser until it knows sessions.</summary>
    public async Task<Dictionary<string, string>> NamesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default) =>
        (await sessions.AllAsync(ct)).Where(s => !s.IsPractice && codes.Contains(s.BoxCode))
            .GroupBy(s => s.BoxCode).ToDictionary(g => g.Key, g => g.First().Title);

    private async Task ChangeAsync(int id, Action<WorkSession> change, CancellationToken ct)
    {
        if (await sessions.GetAsync(id, ct) is not { } s) return;
        change(s);
        await sessions.SaveAsync(s, ct);
    }

    private static T Guard<T>(Func<T> rule)
    {
        try { return rule(); }
        catch (InvalidOperationException ex) { throw new VareinfoException(ex.Message); }
    }
}
