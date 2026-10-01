using System.Text.RegularExpressions;

namespace ITMartinNotatskriver.Server.Services;

/// <summary>Looks for things that should not leave the user's hands: CPR, phone numbers, addresses, e-mails,
/// and health words that suggest notes about a patient. Only a warning - the user decides.</summary>
public static class Sensitive
{
    public sealed record Fund(string Hvad, string Eksempel);

    private static readonly (string Hvad, Regex Rx)[] Personal =
    [
        ("CPR-nummer", new Regex(@"\b[0-3]\d[01]\d\d{2}[- ]?\d{4}\b", RegexOptions.Compiled)),
        ("Telefonnummer", new Regex(@"(?:\+45[ ]?)?\b\d{2}[ ]?\d{2}[ ]?\d{2}[ ]?\d{2}\b", RegexOptions.Compiled)),
        ("E-mail", new Regex(@"\b[\w.+-]+@[\w-]+\.[\w.-]+\b", RegexOptions.Compiled)),
        ("Adresse", new Regex(@"\b[A-ZÆØÅ][a-zæøåé]+(?:vej|gade|allé|alle|vænget|stræde|plads|parken|toften|have|bakken|park|boulevard|torv|sti)\s+\d+", RegexOptions.Compiled)),
    ];

    private static readonly Regex Health = new(
        @"\b(patient|pt\.?|borger(en)?|diagnose|medicin|smerter|BT\s*\d|blodtryk|puls|journal|epj|indlagt|indlæggelse|sygdom|depression|angst|psykiatri|demens|diabetes|insulin|sår|behandling|læge(n)?|sygeplejerske|hjemmepleje|recept|morfin|panodil|paracetamol|operation|scanning|blodprøve|misbrug|selvmord|graviditet|hiv|kræft|cancer)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<Fund> Find(string text)
    {
        var found = new List<Fund>();
        foreach (var (hvad, rx) in Personal)
            if (rx.Match(text) is { Success: true } m) found.Add(new(hvad, m.Value));
        var health = Health.Matches(text).Select(m => m.Value.ToLowerInvariant()).Distinct().Take(4).ToList();
        if (health.Count >= 2) found.Add(new("Helbredsoplysninger", string.Join(", ", health)));
        return found;
    }

    public static bool LooksLikePatientNotes(IEnumerable<Fund> fund) => fund.Any(f => f.Hvad == "Helbredsoplysninger");
}
