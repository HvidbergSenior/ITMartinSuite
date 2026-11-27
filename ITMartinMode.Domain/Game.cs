namespace ITMartinMode.Domain;

/// <summary>
/// The turns of "De 6 HV" (user 2026-11-27): one person tells their idea (no timer). Then the others take turns,
/// 1 minute each, answering one HV-word at a time AS IF it was their own idea: Hvad, Hvorfor, Hvem, Hvor, Hvornår, Hvordan.
/// Next round the next person tells an idea - so everybody starts once and everybody answers all the HV-words.
/// </summary>
public sealed record Seat(string Id, string Name);

public sealed class Game
{
    public static readonly TimeSpan Turn = TimeSpan.FromMinutes(1);
    public const int Steps = 7;   // 0 = the owner tells the idea, 1..6 = the six points

    public IReadOnlyList<Seat> Players { get; }   // in the order they sit (join order)
    public int Round { get; private set; }
    public int Step { get; private set; }
    public DateTimeOffset StepStarted { get; private set; }

    public Game(IReadOnlyList<Seat> players, DateTimeOffset now)
    {
        if (players.Count < 2) throw new DomainException("Der skal være mindst 2 i rummet for at spille.");
        Players = players;
        StepStarted = now;
    }

    public Seat Owner => Players[Round % Players.Count];
    public bool Finished => Round >= Players.Count;

    /// <summary>The point being answered now, or null while the owner tells the idea.</summary>
    public StarPoint? Point => Step == 0 ? null : (StarPoint)(Step - 1);

    /// <summary>Who talks now (during a timed minute ONLY this person's mic is open): the owner at step 0, else the others in turn (the start moves each round).</summary>
    public Seat Speaker
    {
        get
        {
            if (Step == 0) return Owner;
            var others = Players.Where((_, i) => i != Round % Players.Count).ToList();
            return others[(Round + Step - 1) % others.Count];
        }
    }

    public TimeSpan? Left(DateTimeOffset now) => Step == 0 ? null : Turn - (now - StepStarted) is var l && l > TimeSpan.Zero ? l : TimeSpan.Zero;

    public void Next(DateTimeOffset now)
    {
        if (Finished) return;
        Step++;
        if (Step >= Steps) { Step = 0; Round++; }
        StepStarted = now;
    }
}
