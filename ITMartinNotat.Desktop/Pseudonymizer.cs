using System.Text.RegularExpressions;

namespace ITMartinNotat;

/// <summary>
/// Replaces personal data with placeholders ([NAVN1], [CPR1] …) before anything leaves the PC,
/// and puts the real values back into the finished note afterwards. The map only lives in memory.
/// </summary>
public sealed class Pseudonymizer
{
    private readonly Dictionary<string, string> _toPlaceholder = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _toReal = new();
    private readonly Dictionary<string, int> _counters = new();

    public IReadOnlyDictionary<string, string> Map => _toReal;

    // Order matters: CPR before phone/dates, e-mail before names.
    private static readonly (string Kind, Regex Rx)[] Patterns =
    [
        ("CPR", new Regex(@"\b[0-3]\d[01]\d\d{2}[- ]?\d{4}\b", RegexOptions.Compiled)),
        ("EMAIL", new Regex(@"\b[\w.+-]+@[\w-]+\.[\w.-]+\b", RegexOptions.Compiled)),
        ("TELEFON", new Regex(@"(?:\+45[ ]?)?\b\d{2}[ ]?\d{2}[ ]?\d{2}[ ]?\d{2}\b", RegexOptions.Compiled)),
        ("DATO", new Regex(@"\b\d{1,2}[./-]\d{1,2}[./-](?:\d{4}|\d{2})\b", RegexOptions.Compiled)),
        ("DATO", new Regex(@"\b\d{1,2}\.?\s+(?:jan|feb|mar|apr|maj|jun|jul|aug|sep|okt|nov|dec)[a-zæøå]*\.?(?:\s+\d{4})?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("ADRESSE", new Regex(@"\b[A-ZÆØÅ][a-zæøåé]+(?:vej|gade|allé|alle|vænget|stræde|plads|parken|toften|have|bakken|høj|park|boulevard|torv|sti)\s+\d+[A-Za-z]?(?:,?\s*\d+\.?\s*(?:th|tv|mf|sal)\.?)?", RegexOptions.Compiled)),
        ("POSTNR", new Regex(@"\b\d{4}\s+[A-ZÆØÅ][a-zæøå]+(?:\s+[A-ZÆØÅ]\.?)?\b", RegexOptions.Compiled)),
    ];

    /// <summary>Common capitalised words that are not names (start of notes, weekdays, medical words).</summary>
    private static readonly HashSet<string> NotNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pt","Patient","Patienten","Borger","Borgeren","Hun","Han","Der","Det","Den","De","Og","Men","Ved","Ingen","Ikke","Har","Er","Var",
        "Mandag","Tirsdag","Onsdag","Torsdag","Fredag","Lørdag","Søndag","I","Til","Fra","Efter","Før","Under","Over","Med","Uden",
        "Obs","Ok","Status","Plan","Aftale","Aftalt","Samtale","Besøg","Kontrol","Smerter","Medicin","Læge","Lægen","Sygeplejerske",
        "Hjemmepleje","Pårørende","Datter","Søn","Ægtefælle","Hustru","Mand","Kone","Mor","Far","Bror","Søster","Ja","Nej","God","Godt",
        "Dårlig","Træt","Svimmel","Kvalme","Feber","Blodtryk","BT","Puls","Temp","Saturation","Sat","Vægt","Mobilisering","Søvn","Appetit",
        "Humør","Stemning","Rolig","Urolig","Glad","Ked","Forvirret","Klar","Orienteret","Vand","Mad","Bad","Toilet","Bleskift","Sår",
        "Forbinding","Insulin","Paracetamol","Panodil","Morfin","Ring","Ringet","Kontakt","Henvist","Henvisning","Sygehus","Hospital",
        "Region","Kommune","Akut","Subjektivt","Objektivt","Analyse","Vurdering","Handling","Evaluering","Note","Notat",
    };

    public string Hide(string text, IEnumerable<string> extraNames)
    {
        // Names she typed (patient, relatives, staff) - full name first, then each part so "Jensen" alone is caught too.
        var names = extraNames.Select(n => n.Trim()).Where(n => n.Length > 1).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var full in names.OrderByDescending(n => n.Length))
            text = ReplaceWord(text, full, "NAVN");
        foreach (var part in names.SelectMany(n => n.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(p => p.Length > 1).Distinct(StringComparer.OrdinalIgnoreCase))
            if (!NotNames.Contains(part)) text = ReplaceWord(text, part, "NAVN");

        foreach (var (kind, rx) in Patterns)
            text = rx.Replace(text, m => PlaceholderFor(m.Value, kind));
        return text;
    }

    /// <summary>Capitalised words still in the text that might be names - shown so she can tick them away.</summary>
    public static List<string> PossibleNames(string text)
    {
        var found = new List<string>();
        foreach (Match m in Regex.Matches(text, @"(?<![\[\w])[A-ZÆØÅ][a-zæøåéü]{1,}(?:[- ][A-ZÆØÅ][a-zæøåéü]+)*"))
        {
            var w = m.Value;
            if (NotNames.Contains(w) || found.Contains(w, StringComparer.OrdinalIgnoreCase)) continue;
            found.Add(w);
        }
        return found;
    }

    public string Restore(string text, out List<string> missing)
    {
        missing = [];
        foreach (var (ph, real) in _toReal)
        {
            if (!text.Contains(ph)) { missing.Add($"{ph} = {real}"); continue; }
            text = text.Replace(ph, real);
        }
        return text;
    }

    private string ReplaceWord(string text, string word, string kind) =>
        // "Jensens" -> "[NAVN1]s": the possessive stays outside so one person keeps one placeholder.
        Regex.Replace(text, $@"(?<![\w\[])({Regex.Escape(word)})('?s)?(?![\w\]])", m => PlaceholderFor(m.Groups[1].Value, kind) + m.Groups[2].Value, RegexOptions.IgnoreCase);

    private string PlaceholderFor(string real, string kind)
    {
        if (_toPlaceholder.TryGetValue(real, out var existing)) return existing;
        var n = _counters.GetValueOrDefault(kind) + 1;
        _counters[kind] = n;
        var ph = $"[{kind}{n}]";
        _toPlaceholder[real] = ph;
        _toReal[ph] = real;
        return ph;
    }
}
