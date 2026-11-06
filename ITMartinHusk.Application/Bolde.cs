using ITMartinHusk.Domain;

namespace ITMartinHusk.Application;

/// <summary>What a helper (Claude, for Martin) sends to make or update a ball. Matched on <see cref="Key"/>; null = keep.</summary>
public sealed record BallInput(
    string? Key, string? Title, string? Area = null, string? Status = null, string? NextStep = null, string? WaitingOn = null,
    string? Note = null, DateTime? Deadline = null, bool ClearDeadline = false, string? Link = null);

public sealed record BallSaveResult(int Made, int Changed, List<string> Errors);

/// <summary>Bolde i luften: every open thing, one next step each, one ball at a time on the front page.</summary>
public sealed class BallUseCases(IHuskStore store, Func<DateTime> now)
{
    /// <summary>The ONE ball to work on now (skipping those the person said "Ikke nu" to this visit), or null.</summary>
    public Ball? Next(IReadOnlyCollection<Guid>? skip = null) =>
        BallRules.Order(store.Get().Balls, now()).FirstOrDefault(b => skip is null || !skip.Contains(b.Id));

    public List<BallGroup> Groups() => BallRules.Groups(store.Get().Balls, now());

    public Ball? Get(Guid id) => store.Get().Balls.FirstOrDefault(b => b.Id == id);

    public List<Ball> All() => store.Get().Balls.OrderBy(b => b.Title).ToList();

    /// <summary>A new ball from the page. Null when saved, else the reason in plain Danish.</summary>
    public string? Add(string? title, string? area, string? nextStep, string? waitingOn, DateTime? deadline)
    {
        try
        {
            var b = Ball.Create(title, null, now());
            b.Change(new BallChange(Area: area, NextStep: nextStep, WaitingOn: waitingOn, Deadline: deadline), now());
            if (b.WaitingOn.Length > 0 && b.NextStep.Length == 0) b.SetStatus(BallStatus.Waiting, now());
            store.Update(d =>
            {
                b.Key = UniqueKey(d, b.Key);
                d.Balls.Add(b);
            });
            return null;
        }
        catch (ArgumentException e) { return e.Message; }
    }

    public string? Change(Guid id, BallChange change)
    {
        string? error = null;
        store.Update(d =>
        {
            try { d.Balls.FirstOrDefault(b => b.Id == id)?.Change(change, now()); }
            catch (ArgumentException e) { error = e.Message; }
        });
        return error;
    }

    public void SetStatus(Guid id, BallStatus s) => store.Update(d => d.Balls.FirstOrDefault(b => b.Id == id)?.SetStatus(s, now()));

    /// <summary>"Det er det næste": only one ball is picked at a time.</summary>
    public void Pin(Guid id) => store.Update(d =>
    {
        foreach (var b in d.Balls) b.Pinned = b.Id == id && b.Status == BallStatus.InProgress;
    });

    /// <summary>"Gjort": the step is done. A new step keeps the ball going; none and nobody to wait for = the ball is done.</summary>
    public string? StepDone(Guid id, string? newStep, string? waitingOn)
    {
        var step = HuskRules.Clean(newStep, Ball.MaxText);
        var wait = HuskRules.Clean(waitingOn, Ball.MaxTitle);
        var status = step.Length > 0 ? BallStatus.InProgress : wait.Length > 0 ? BallStatus.Waiting : BallStatus.Done;
        return Change(id, new BallChange(Status: status, NextStep: step, WaitingOn: wait));
    }

    public void Remove(Guid id) => store.Update(d => d.Balls.RemoveAll(b => b.Id == id));

    /// <summary>Makes or updates many balls at once, matched on Key (the API Claude uses at the end of a session).</summary>
    public BallSaveResult Save(IEnumerable<BallInput> input)
    {
        int made = 0, changed = 0;
        var errors = new List<string>();
        store.Update(d =>
        {
            foreach (var i in input)
            {
                var key = BallRules.Key(i.Key ?? i.Title);
                if (key.Length == 0) { errors.Add("En bold mangler key eller titel."); continue; }
                BallStatus? status = null;
                if (i.Status is not null && (status = BallRules.ParseStatus(i.Status)) is null) { errors.Add($"{key}: ukendt status '{i.Status}'."); continue; }
                try
                {
                    var b = d.Balls.FirstOrDefault(x => x.Key == key);
                    var isNew = b is null;
                    b ??= Ball.Create(i.Title, key, now());
                    b.Change(new BallChange(i.Title, i.Area, status, i.NextStep, i.WaitingOn, i.Note, i.Deadline, i.ClearDeadline, i.Link), now());
                    if (isNew) { d.Balls.Add(b); made++; } else changed++;
                }
                catch (ArgumentException e) { errors.Add($"{key}: {e.Message}"); }
            }
        });
        return new BallSaveResult(made, changed, errors);
    }

    private static string UniqueKey(HuskData d, string key)
    {
        var k = key;
        for (var n = 2; d.Balls.Any(b => b.Key == k); n++) k = $"{key}-{n}";
        return k;
    }
}
