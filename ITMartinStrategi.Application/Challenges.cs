using ITMartinStrategi.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinStrategi.Application;

/// <summary>Every digital Wingspan bird, found by name - typos allowed.</summary>
public interface IWingspanCards
{
    IReadOnlyList<WingspanBird> All { get; }
    /// <summary>The bird, or null and up to 3 close names.</summary>
    (WingspanBird? Bird, IReadOnlyList<string> Suggestions) Find(string name);
}

/// <summary>The saved challenge weeks.</summary>
public interface IChallengeStore
{
    IReadOnlyList<WingspanChallenge> All();
    WingspanChallenge? Get(Guid id);
    void Save(WingspanChallenge challenge);
    void Delete(Guid id);
}

/// <summary>The birds typed in, matched to cards: the ones found, and the names that were not with suggestions.</summary>
public sealed record BirdMatch(IReadOnlyList<WingspanBird> Found, IReadOnlyList<(string Typed, IReadOnlyList<string> Suggestions)> Unknown);

/// <summary>Wingspan "Champ of the Birds" weeks (user 2026-10-29: "i will write the names of the birds and in strategi app this
/// should be written down"): save a week's birds and attempts, and plan it with ChallengeAdvisor. The page only shows.</summary>
public sealed class ChallengeUseCases(IWingspanCards cards, IChallengeStore store)
{
    public IReadOnlyList<WingspanChallenge> Weeks() => store.All().OrderByDescending(c => c.CreatedUtc).ToList();

    public WingspanChallenge? Week(Guid id) => store.Get(id);

    /// <summary>Names separated by commas or new lines; "Red-backed" finds Red-Backed Fairywren.</summary>
    public static List<string> SplitNames(string text) =>
        text.Split([',', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(n => n.Length > 1).ToList();

    public BirdMatch Match(IEnumerable<string> names)
    {
        var found = new List<WingspanBird>();
        var unknown = new List<(string, IReadOnlyList<string>)>();
        foreach (var n in names)
        {
            var (bird, suggestions) = cards.Find(n);
            if (bird is null) unknown.Add((n, suggestions));
            else if (!found.Contains(bird)) found.Add(bird);
        }
        return new BirdMatch(found, unknown);
    }

    public ChallengePlan Plan(WingspanChallenge week) => ChallengeAdvisor.Plan(Match(week.StartBirds.Concat(week.SeenBirds)).Found);

    /// <summary>A new week from the start hand; the typed names are stored as the cards' own names when found.</summary>
    public WingspanChallenge Create(string week, string startBirds)
    {
        var c = new WingspanChallenge { Week = week.Trim().Length > 0 ? week.Trim() : DateTime.Today.ToString("yyyy-MM-dd") };
        c.StartBirds = Normalise(SplitNames(startBirds));
        store.Save(c);
        return c;
    }

    public void AddSeen(Guid id, string birds) => Change(id, c =>
    {
        foreach (var n in Normalise(SplitNames(birds)))
            if (!c.StartBirds.Contains(n) && !c.SeenBirds.Contains(n)) c.SeenBirds.Add(n);
    });

    public void RemoveBird(Guid id, string name) => Change(id, c => { c.StartBirds.Remove(name); c.SeenBirds.Remove(name); });

    public void AddAttempt(Guid id, int score, string note) =>
        Change(id, c => c.Attempts.Add(new ChallengeAttempt { Score = Math.Clamp(score, 0, 400), Note = note.Trim() }));

    public void SetNote(Guid id, string note) => Change(id, c => c.Note = note.Trim());

    public void Delete(Guid id) => store.Delete(id);

    private List<string> Normalise(IEnumerable<string> names) =>
        names.Select(n => cards.Find(n).Bird?.Name ?? n).Distinct().ToList();

    private void Change(Guid id, Action<WingspanChallenge> change)
    {
        if (store.Get(id) is not { } c) return;
        change(c);
        store.Save(c);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddStrategiApplication(this IServiceCollection services) =>
        services.AddScoped<ChallengeUseCases>();
}
