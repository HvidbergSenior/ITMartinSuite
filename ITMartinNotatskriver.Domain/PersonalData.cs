using System.Text.RegularExpressions;

namespace ITMartinNotatskriver.Domain;

/// <summary>Something in the notes that should not leave the user's hands, with an example of what was seen.</summary>
public sealed record Finding(string What, string Example)
{
    public const string Health = "Helbredsoplysninger";
}

/// <summary>Looks for CPR numbers, phone numbers, addresses, e-mails and health words that suggest notes about a
/// patient or citizen. Patient notes are a HARD stop (they must never reach the AI); other personal data is a stop
/// until the user removes it or lets the names be hidden.</summary>
public static partial class PersonalData
{
    private static readonly (string What, Regex Rx)[] Personal =
    [
        ("CPR-nummer", Cpr()),
        ("Telefonnummer", Phone()),
        ("E-mail", Email()),
        ("Adresse", Address()),
    ];

    public static IReadOnlyList<Finding> Find(string text)
    {
        var found = new List<Finding>();
        foreach (var (what, rx) in Personal)
            if (rx.Match(text) is { Success: true } m) found.Add(new(what, m.Value));
        var health = HealthWords().Matches(text).Select(m => m.Value.ToLowerInvariant()).Distinct().Take(4).ToList();
        if (health.Count >= 2) found.Add(new(Finding.Health, string.Join(", ", health)));
        return found;
    }

    public static bool LooksLikePatientNotes(IEnumerable<Finding> findings) => findings.Any(f => f.What == Finding.Health);

    [GeneratedRegex(@"\b[0-3]\d[01]\d\d{2}[- ]?\d{4}\b")]
    private static partial Regex Cpr();

    [GeneratedRegex(@"(?:\+45[ ]?)?\b\d{2}[ ]?\d{2}[ ]?\d{2}[ ]?\d{2}\b")]
    private static partial Regex Phone();

    [GeneratedRegex(@"\b[\w.+-]+@[\w-]+\.[\w.-]+\b")]
    private static partial Regex Email();

    [GeneratedRegex(@"\b[A-ZÆØÅ][a-zæøåé]+(?:vej|gade|allé|alle|vænget|stræde|plads|parken|toften|have|bakken|park|boulevard|torv|sti)\s+\d+")]
    private static partial Regex Address();

    [GeneratedRegex(@"\b(patient|pt\.?|borger(en)?|diagnose|medicin|smerter|BT\s*\d|blodtryk|puls|journal|epj|indlagt|indlæggelse|sygdom|depression|angst|psykiatri|demens|diabetes|insulin|sår|behandling|læge(n)?|sygeplejerske|hjemmepleje|recept|morfin|panodil|paracetamol|operation|scanning|blodprøve|misbrug|selvmord|graviditet|hiv|kræft|cancer)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HealthWords();
}
