namespace ITMartinLager.Domain;

// Grand Comics Database (2026-10-25, user: "i want all"): the AI reads a comic's series and number but often not the
// year - and an Anders And blad or an album is searched with its year. GCD knows the Danish series issue by issue
// ("Anders And & Co. (1949 series)", issue "1/1949", key date 1949-03). The year is only filled in when ONE Danish
// series has that number - several = do not guess.

/// <summary>One series as GCD has it: its issues' descriptors ("12", "1/1949", "1 - Manden med de to ansigter").</summary>
public sealed record CatalogSeries(string Name, string Country, int? YearBegan, int? YearEnded, IReadOnlyList<string> Descriptors,
    IReadOnlyList<string> IssueUrls);

public static class ComicCatalog
{
    /// <summary>The number at the start of a descriptor: "12" -> "12", "1/1949" -> "1", "3 - Titel" -> "3", "[1993]" -> "".</summary>
    public static string NumberOf(string descriptor)
    {
        var d = descriptor.Trim();
        var n = new string(d.TakeWhile(char.IsAsciiDigit).ToArray());
        return n.TrimStart('0') is { Length: > 0 } t ? t : n;
    }

    /// <summary>The year in a descriptor like "1/1949", or null.</summary>
    public static int? YearIn(string descriptor)
    {
        var m = System.Text.RegularExpressions.Regex.Match(descriptor, @"/\s*(1[89]\d\d|20\d\d)\b");
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    /// <summary>The one issue with this number in the Danish series of exactly this name: (series, index of the issue).
    /// Null when none - or more than one, unless they all say the same year.</summary>
    public static (CatalogSeries Series, int Index)? Pick(IEnumerable<CatalogSeries> series, string name, string number)
    {
        var want = NumberOf(number);
        if (want == "") return null;
        var hits = series
            .Where(s => s.Country == "dk" && Same(s.Name, name))
            .SelectMany(s => s.Descriptors.Select((d, i) => (s, i, d)))
            .Where(x => NumberOf(x.d) == want)
            .ToList();
        if (hits.Count == 1) return (hits[0].s, hits[0].i);
        // Several hits that all carry the same year in the descriptor are still one answer for the year.
        var years = hits.Select(h => YearIn(h.d) ?? (h.s.YearBegan == h.s.YearEnded ? h.s.YearBegan : null)).Distinct().ToList();
        return hits.Count > 1 && years.Count == 1 && years[0] is not null ? (hits[0].s, hits[0].i) : null;
    }

    /// <summary>The year from what is known without asking for the issue itself: the descriptor, or a one-year series.</summary>
    public static int? YearWithoutIssue(CatalogSeries s, int index) =>
        YearIn(s.Descriptors[index]) ?? (s.YearBegan is { } b && s.YearBegan == s.YearEnded ? b : null);

    /// <summary>"1968-03-00" -> 1968.</summary>
    public static int? YearOfKeyDate(string? keyDate) =>
        keyDate is { Length: >= 4 } k && int.TryParse(k[..4], out var y) && y > 1800 ? y : null;

    private static bool Same(string a, string b) => Key(a) == Key(b);
    private static string Key(string s) => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
