using ITMartinVurdering.Domain;

namespace ITMartinVurdering.Application;

/// <summary>Every Sag with its things, and the photos - one folder per Sag on the data volume.</summary>
public interface ISagStore
{
    List<Sag> All();
    Sag? Get(string id);
    void Save(Sag sag);
    void SavePhoto(string sagId, string tingId, byte[] jpeg);
    byte[]? Photo(string sagId, string tingId);
    void DeletePhoto(string sagId, string tingId);
}

/// <summary>One photo to value, numbered in the call.</summary>
public sealed record ThingPhoto(int Index, byte[] Jpeg, string Hint);

/// <summary>What the AI says about one numbered photo. Verdict = sælg / sælg samlet / giv væk / smid ud / spørg en ekspert.</summary>
public sealed record ThingValue(int Index, string What, string Details, int? Low, int? High, string Verdict, string Where,
    string Reason, string SearchWords);

/// <summary>Values several photos in ONE call, under a hard daily cap (Vurdering:MaxAiCallsPerDay).</summary>
public interface IThingValuer
{
    bool Enabled { get; }
    int CallsLeftToday();
    Task<List<ThingValue>> ValueAsync(IReadOnlyList<ThingPhoto> photos, CancellationToken ct);
}

public sealed record DodsboResult(bool Ok, string Message);

/// <summary>Dødsbo og loft: make a Sag, add a photo per thing, value the waiting ones (one AI call for up to 8), correct by hand.</summary>
public sealed class DodsboUseCases(ISagStore store, IThingValuer valuer, Func<DateTime> now)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public List<Sag> All() => store.All().OrderByDescending(s => s.CreatedAt).ToList();
    public Sag? Get(string id) => store.Get(id);

    public (Sag? Sag, string? Error) New(string? name)
    {
        try
        {
            var s = Sag.New(name, now());
            store.Save(s);
            return (s, null);
        }
        catch (ArgumentException e) { return (null, e.Message); }
    }

    public DodsboResult AddPhoto(string sagId, byte[] jpeg, string? hint)
    {
        if (store.Get(sagId) is not { } s) return new(false, "Sagen findes ikke længere.");
        if (jpeg.Length == 0) return new(false, "Billedet er tomt – tag det igen.");
        var t = new Ting { AddedAt = now(), Hint = DodsboRules.Clean(hint, DodsboRules.MaxText) };
        store.SavePhoto(sagId, t.Id, jpeg);
        s.Ting.Add(t);
        store.Save(s);
        return new(true, "Billedet er gemt – det venter på at blive vurderet.");
    }

    /// <summary>"Vurdér de nye": the next waiting photos (at most PerCall) in ONE AI call. Never more than one call per press.</summary>
    public async Task<DodsboResult> ValueWaitingAsync(string sagId, CancellationToken ct = default)
    {
        if (!valuer.Enabled) return new(false, "AI er ikke slået til (Claude:ApiKey mangler).");
        if (!await Gate.WaitAsync(0, ct)) return new(false, "Der bliver allerede vurderet – vent et øjeblik.");
        try
        {
            if (store.Get(sagId) is not { } s) return new(false, "Sagen findes ikke længere.");
            if (valuer.CallsLeftToday() <= 0) return new(false, "Dagens grænse for AI-vurderinger er nået. Prøv igen i morgen.");
            var waiting = s.Ting.Where(t => t.Waiting).Take(DodsboRules.PerCall).ToList();
            if (waiting.Count == 0) return new(true, "Der er ikke nogen nye billeder at vurdere.");
            var photos = waiting.Select((t, i) => (t, i, jpeg: store.Photo(sagId, t.Id))).Where(x => x.jpeg is { Length: > 0 })
                .Select(x => new ThingPhoto(x.i, x.jpeg!, x.t.Hint)).ToList();
            var answers = await valuer.ValueAsync(photos, ct);
            var at = now();
            var done = 0;
            foreach (var a in answers)
            {
                if (a.Index < 0 || a.Index >= waiting.Count) continue;
                waiting[a.Index].Valued(a.What, a.Details, a.Low, a.High, DodsboRules.ParseVerdict(a.Verdict) ?? Verdict.SpoergEkspert,
                    a.Where, a.Reason, a.SearchWords, at);
                done++;
            }
            store.Save(s);
            var left = s.Ting.Count(t => t.Waiting);
            return new(true, $"{done} ting er vurderet." + (left > 0 ? $" {left} venter stadig – tryk igen." : ""));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new(false, "Vurderingen gik galt: " + e.Message);
        }
        finally { Gate.Release(); }
    }

    /// <summary>The person's own price and note (empty price = use the AI's range again).</summary>
    public DodsboResult Own(string sagId, string tingId, int? kr, string? note)
    {
        if (store.Get(sagId) is not { } s || s.Ting.FirstOrDefault(t => t.Id == tingId) is not { } t) return new(false, "Tingen findes ikke længere.");
        if (kr is < 0) return new(false, "Prisen kan ikke være under 0 kr.");
        t.OwnKr = kr is > 0 ? kr : null;
        t.OwnNote = DodsboRules.Clean(note, DodsboRules.MaxText);
        store.Save(s);
        return new(true, "Gemt.");
    }

    public DodsboResult Remove(string sagId, string tingId)
    {
        if (store.Get(sagId) is not { } s) return new(false, "Sagen findes ikke længere.");
        if (s.Ting.RemoveAll(t => t.Id == tingId) == 0) return new(false, "Tingen findes ikke længere.");
        store.DeletePhoto(sagId, tingId);
        store.Save(s);
        return new(true, "Tingen er fjernet.");
    }

    public byte[]? Photo(string sagId, string tingId) => store.Photo(sagId, tingId);
}
