using ITMartinVareinfo.Domain;

namespace ITMartinVareinfo.Application;

/// <summary>The sessions Martin prepares (user 2026-10-26: "I prepare a session with 4 helpers. I point to 20 boxes and
/// name them"). One shared pool: every helper sees the boxes of the open sessions and taps a free one.</summary>
public interface ISessionRepository
{
    /// <summary>Newest first, with their boxes.</summary>
    Task<List<WorkSession>> AllAsync(CancellationToken ct);
    Task<WorkSession?> GetAsync(int id, CancellationToken ct);
    /// <summary>Adds (Id 0) or updates, boxes included.</summary>
    Task SaveAsync(WorkSession session, CancellationToken ct);
    Task<SessionBox?> BoxAsync(int boxId, CancellationToken ct);
    Task UpdateBoxAsync(SessionBox box, CancellationToken ct);
    Task RemoveBoxAsync(int boxId, CancellationToken ct);
    /// <summary>Every box number in any session (open or closed).</summary>
    Task<List<int>> UsedNumbersAsync(CancellationToken ct);
}

public sealed class SessionUseCases(ISessionRepository sessions, IBoxClaimRepository claims, IPhotoRepository photos, TimeProvider clock)
{
    /// <summary>New boxes made at a time ("Lav 20 nye kasser").</summary>
    public const int MaxNewAtOnce = 100;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public Task<List<WorkSession>> AllAsync(CancellationToken ct = default) => sessions.AllAsync(ct);

    public async Task<WorkSession> StartAsync(string? name, CancellationToken ct = default)
    {
        var s = WorkSession.Start(name, Now);
        await sessions.SaveAsync(s, ct);
        return s;
    }

    /// <summary>"Lav nye kasser": count boxes with the next free numbers - write the numbers on the boxes (or print
    /// their labels in Lager). Returns the numbers.</summary>
    public async Task<List<int>> NewBoxesAsync(int sessionId, int count, CancellationToken ct = default)
    {
        if (count is < 1 or > MaxNewAtOnce) throw new VareinfoException($"Skriv hvor mange kasser (1-{MaxNewAtOnce}).");
        var s = await OpenSessionAsync(sessionId, ct);
        var used = (await sessions.UsedNumbersAsync(ct)).Concat((await claims.AllAsync(ct)).Select(c => c.Number));
        var numbers = Guard(() => WorkSession.NextNumbers(used, count));
        foreach (var n in numbers) Guard(() => s.Add(n, null));
        await sessions.SaveAsync(s, ct);
        return numbers;
    }

    /// <summary>"Tilføj en kasse": a box that already has a number (written on it, or from an earlier session).</summary>
    public async Task AddBoxAsync(int sessionId, int number, string? name, CancellationToken ct = default)
    {
        var s = await OpenSessionAsync(sessionId, ct);
        Guard(() => s.Add(number, name));
        await sessions.SaveAsync(s, ct);
    }

    public async Task RenameBoxAsync(int boxId, string? name, CancellationToken ct = default)
    {
        if (await sessions.BoxAsync(boxId, ct) is not { } box) return;
        box.Name = (name ?? "").Trim();
        await sessions.UpdateBoxAsync(box, ct);
    }

    /// <summary>Out of the session - the photos already taken in it are kept.</summary>
    public Task RemoveBoxAsync(int boxId, CancellationToken ct = default) => sessions.RemoveBoxAsync(boxId, ct);

    /// <summary>Closed: its boxes leave the helpers' list (the photos are kept).</summary>
    public async Task CloseAsync(int sessionId, CancellationToken ct = default)
    {
        if (await sessions.GetAsync(sessionId, ct) is not { } s) return;
        s.Close(Now);
        await sessions.SaveAsync(s, ct);
    }

    public async Task ReopenAsync(int sessionId, CancellationToken ct = default)
    {
        if (await sessions.GetAsync(sessionId, ct) is not { } s) return;
        s.ClosedAt = null;
        await sessions.SaveAsync(s, ct);
    }

    /// <summary>The helper's list: the boxes of every open session, each free / theirs / someone else's / done.</summary>
    public async Task<List<BoxChoice>> ChoicesAsync(Taker me, CancellationToken ct = default)
    {
        var open = await OpenBoxesAsync(ct);
        if (open.Count == 0) return [];
        var all = (await claims.AllAsync(ct)).ToDictionary(c => c.BoxCode);
        var counts = await photos.PerBoxAsync(ct);
        var now = Now;
        return open.Select(b => BoxChoice.For(b, all.GetValueOrDefault(b.BoxCode), me, now, counts.GetValueOrDefault(b.BoxCode))).ToList();
    }

    /// <summary>Is this box in an open session? (a helper may only take those)</summary>
    public async Task<bool> IsOpenAsync(string boxCode, CancellationToken ct = default) =>
        (await OpenBoxesAsync(ct)).Any(b => b.BoxCode == boxCode);

    /// <summary>The names Martin gave these boxes (the newest session's name wins) - Lager shows them on Kasser.</summary>
    public async Task<Dictionary<string, string>> NamesAsync(IReadOnlyCollection<string> boxCodes, CancellationToken ct = default) =>
        (await sessions.AllAsync(ct)).SelectMany(s => s.Boxes)
            .Where(b => b.Name != "" && boxCodes.Contains(b.BoxCode))
            .GroupBy(b => b.BoxCode).ToDictionary(g => g.Key, g => g.First().Name);

    private async Task<List<SessionBox>> OpenBoxesAsync(CancellationToken ct) =>
        (await sessions.AllAsync(ct)).Where(s => s.Open).OrderBy(s => s.CreatedAt)
            .SelectMany(s => s.Boxes.OrderBy(b => b.SortOrder)).DistinctBy(b => b.BoxCode).ToList();

    private async Task<WorkSession> OpenSessionAsync(int id, CancellationToken ct)
    {
        var s = await sessions.GetAsync(id, ct) ?? throw new VareinfoException("Sessionen findes ikke.");
        if (!s.Open) throw new VareinfoException("Sessionen er lukket - åbn den igen først.");
        return s;
    }

    private static T Guard<T>(Func<T> rule)
    {
        try { return rule(); }
        catch (InvalidOperationException ex) { throw new VareinfoException(ex.Message); }
    }
}
