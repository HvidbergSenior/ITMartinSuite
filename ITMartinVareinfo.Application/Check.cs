using ITMartinVareinfo.Domain;

namespace ITMartinVareinfo.Application;

/// <summary>Lager's answers for a Tjek session (user 2026-11-10: "Take picture -> get quick price and/or if something is
/// interesting"). Lager reads and prices; Vareinfo only shows the answers and passes on what the person pressed.</summary>
public interface ICheckAnswers
{
    /// <summary>Null = Lager did not answer (down, or the key is wrong).</summary>
    Task<CheckAnswers?> ForAsync(string boxCode, CancellationToken ct);
    /// <summary>action = "tog", "nej" or "fortryd". False = Lager refused or did not answer.</summary>
    Task<bool> PressAsync(string boxCode, int itemId, string action, CancellationToken ct);
    Task<byte[]?> PhotoAsync(string boxCode, int itemId, CancellationToken ct);
}

public sealed class CheckUseCases(ICheckAnswers lager, SessionUseCases sessions, CaptureUseCases capture)
{
    public const string Take = "tog", No = "nej", Back = "fortryd";

    /// <summary>The answers for a Tjek session this person may use; null when it is not one, or Lager is not answering.</summary>
    public async Task<CheckAnswers?> AnswersAsync(int sessionId, Taker me, CancellationToken ct = default) =>
        await TjekAsync(sessionId, me, ct) is { } s ? await lager.ForAsync(s.BoxCode, ct) : null;

    /// <summary>Tog den / Nej tak / Fortryd on one thing of the session. Returns a message for the person when it did not go through.</summary>
    public async Task<string?> PressAsync(int sessionId, int itemId, string action, Taker me, CancellationToken ct = default)
    {
        if (action is not (Take or No or Back)) return "Ukendt knap.";
        if (await TjekAsync(sessionId, me, ct) is not { } s) return "Den tjek-session findes ikke.";
        return await lager.PressAsync(s.BoxCode, itemId, action, ct) ? null : "Lager svarede ikke – prøv igen om lidt.";
    }

    /// <summary>The thing's own picture (cut out by Lager), else the pile photo it was read from (ours).</summary>
    public async Task<(byte[]? Jpeg, string? File)> PhotoAsync(int sessionId, int itemId, int? pilePhotoId, Taker me, CancellationToken ct = default)
    {
        if (await TjekAsync(sessionId, me, ct) is not { } s) return (null, null);
        if (await lager.PhotoAsync(s.BoxCode, itemId, ct) is { } own) return (own, null);
        return pilePhotoId is { } p ? (null, await capture.PhotoFileAsync(p, null, ct)) : (null, null);
    }

    // Open or closed (yesterday's Tjek can still be answered), but it must be a Tjek session for this person.
    private async Task<WorkSession?> TjekAsync(int sessionId, Taker me, CancellationToken ct) =>
        await sessions.ForAsync(sessionId, me, ct) is { Fund: true } s ? s : null;
}
